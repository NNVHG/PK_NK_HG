using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.MOD_MST.Services;

public sealed class DentalChairService(IDentalChairRepository chairs, IUnitOfWork uow, IAuditLogger audit, VietnamClock clock)
{
    public static readonly Error DuplicateNumber = new("MST_051", "Mã phòng đã tồn tại.");
    public static readonly Error DuplicateRoomName = new("MST_055", "Tên phòng điều trị đã tồn tại.");
    public static readonly Error Changed = new("MST_052", "Dữ liệu phòng đã thay đổi hoặc có thao tác đồng thời. Tải lại trước khi xác nhận.");
    public static readonly Error Assigned = new("MST_054", "Phòng đã được nha sĩ chọn làm việc hôm nay. Cần gỡ liên kết trước khi sửa danh mục.");
    public static readonly Error Occupied = new("MST_053", "Phòng đang phục vụ ca khám, chưa thể sửa danh mục hoặc giải phóng thủ công.");
    public async Task<Result<PagedResult<DentalChairResponse>>> GetPageAsync(string? role, DentalChairQuery request, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Assistant)) return Result<PagedResult<DentalChairResponse>>.Failure(Error.Forbidden);
        if (!(await new DentalChairQueryValidator().ValidateAsync(request, ct)).IsValid) return Result<PagedResult<DentalChairResponse>>.Failure(Error.Validation);
        var page = await chairs.GetPageAsync(request, ct);
        return Result<PagedResult<DentalChairResponse>>.Success(PagedResult<DentalChairResponse>.Create(page.Items.Select(ToResponse).ToList(), page.TotalCount, page.Page, page.PageSize));
    }
    public async Task<Result<DentalChairResponse>> CreateAsync(int actorId, string? role, CreateDentalChairRequest request, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin) return Result<DentalChairResponse>.Failure(Error.Forbidden);
        if (actorId <= 0 || !(await new CreateDentalChairRequestValidator().ValidateAsync(request, ct)).IsValid) return Result<DentalChairResponse>.Failure(Error.Validation);
        try
        {
            return await uow.ExecuteSerializableTransactionAsync(async transactionCt =>
            {
                var number = request.ChairNumber.Trim().ToUpperInvariant();
                var cleanName = ClinicRoom.CleanDisplayRoomName(request.RoomName);
                var normalizedName = ClinicRoom.NormalizeRoomName(request.RoomName);
                if (string.IsNullOrWhiteSpace(cleanName)) return Result<DentalChairResponse>.Failure(Error.Validation);
                if (await chairs.NumberExistsAsync(number, transactionCt)) return Result<DentalChairResponse>.Failure(DuplicateNumber);
                if (await chairs.RoomNameExistsAsync(normalizedName, null, transactionCt)) return Result<DentalChairResponse>.Failure(DuplicateRoomName);
                var room = new ClinicRoom
                {
                    RoomCode = number, RoomName = cleanName, NormalizedRoomName = normalizedName,
                    Status = (DentalChairStatus)request.Status, Description = Normalize(request.Description),
                    IsActive = request.Status != 4, CreatedByUserId = actorId, CreatedAt = clock.UtcNow
                };
                await chairs.AddAsync(room, transactionCt); await uow.SaveChangesAsync(transactionCt);
                await audit.LogRequiredAsync("MOD_MST_CHAIR_CREATED", "ClinicRoom", room.Id, actorId,
                    JsonSerializer.Serialize(new { status = request.Status, version = room.Version }), ct: transactionCt);
                return Result<DentalChairResponse>.Success(ToResponse(room));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Result<DentalChairResponse>.Failure(Changed); }
    }
    public async Task<Result<DentalChairResponse>> UpdateAsync(int id, int actorId, string? role, UpdateDentalChairRequest request, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin) return Result<DentalChairResponse>.Failure(Error.Forbidden);
        if (id <= 0 || actorId <= 0 || !(await new UpdateDentalChairRequestValidator().ValidateAsync(request, ct)).IsValid) return Result<DentalChairResponse>.Failure(Error.Validation);
        try
        {
            return await uow.ExecuteSerializableTransactionAsync(async transactionCt =>
            {
                var room = await chairs.FindAsync(id, transactionCt);
                if (room is null) return Result<DentalChairResponse>.Failure(Error.NotFound);
                if (room.Version != request.ExpectedVersion) return Result<DentalChairResponse>.Failure(Changed);
                if (room.Status == DentalChairStatus.Occupied ||
                    await chairs.HasActiveVisitAsync(room.Id, transactionCt))
                    return Result<DentalChairResponse>.Failure(Occupied);
                if (await chairs.IsAssignedAsync(id, transactionCt)) return Result<DentalChairResponse>.Failure(Assigned);
                var cleanName = ClinicRoom.CleanDisplayRoomName(request.RoomName);
                var normalizedName = ClinicRoom.NormalizeRoomName(request.RoomName);
                if (string.IsNullOrWhiteSpace(cleanName)) return Result<DentalChairResponse>.Failure(Error.Validation);
                if (await chairs.RoomNameExistsAsync(normalizedName, id, transactionCt)) return Result<DentalChairResponse>.Failure(DuplicateRoomName);
                var previous = (int)room.Status;
                room.RoomName = cleanName; room.NormalizedRoomName = normalizedName; room.Description = Normalize(request.Description);
                room.Status = (DentalChairStatus)request.Status; room.IsActive = request.Status != 4;
                room.UpdatedAt = clock.UtcNow; room.UpdatedByUserId = actorId; room.Version++;
                await uow.SaveChangesAsync(transactionCt);
                await audit.LogRequiredAsync("MOD_MST_CHAIR_UPDATED", "ClinicRoom", room.Id, actorId,
                    JsonSerializer.Serialize(new { previousStatus = previous, status = request.Status, version = room.Version,
                        changedFields = new[] { "RoomName", "Description", "Status", "IsActive" } }), ct: transactionCt);
                return Result<DentalChairResponse>.Success(ToResponse(room));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Result<DentalChairResponse>.Failure(Changed); }
    }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DentalChairResponse ToResponse(ClinicRoom x) => new(x.Id, x.RoomCode, x.RoomName, (int)x.Status,
        x.Status switch { DentalChairStatus.Available => "Sẵn sàng", DentalChairStatus.Occupied => "Đang phục vụ", DentalChairStatus.Maintenance => "Bảo trì", DentalChairStatus.Inactive => "Ngừng sử dụng", _ => "Không xác định" },
        x.Description, x.IsActive, x.Version, x.CreatedAt, x.UpdatedAt);
}
