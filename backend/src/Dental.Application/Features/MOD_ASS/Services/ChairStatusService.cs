using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_ASS.DTOs;
using Dental.Application.Features.MOD_ASS.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Enums;

namespace Dental.Application.Features.MOD_ASS.Services;

public sealed class ChairStatusService(IChairStatusRepository chairs, IUnitOfWork uow, IAuditLogger audit, VietnamClock clock)
{
    public static readonly Error Changed = new("ASS_061", "Trạng thái ghế đã thay đổi hoặc có thao tác đồng thời. Tải lại rồi xác nhận lại.");
    public static readonly Error Occupied = new("ASS_062", "Ghế đang phục vụ ca khám. Chỉ luồng khám được giải phóng ghế.");
    public static readonly Error Assigned = new("ASS_063", "Nha sĩ/Admin cần bỏ hoặc đổi vị trí khỏi ghế này trước khi đổi trạng thái.");
    private static bool RoleAllowed(string? role) => role is RoleCodes.Admin or RoleCodes.Assistant;

    public async Task<Result<PagedResult<ChairStatusResponse>>> GetPageAsync(int actorId, string? role, ChairStatusQuery query, CancellationToken ct = default)
    {
        if (!RoleAllowed(role) || actorId <= 0 || !await chairs.ActorAllowedAsync(actorId, role!, ct))
            return Result<PagedResult<ChairStatusResponse>>.Failure(Error.Forbidden);
        if (!(await new ChairStatusQueryValidator().ValidateAsync(query, ct)).IsValid)
            return Result<PagedResult<ChairStatusResponse>>.Failure(Error.Validation);
        return Result<PagedResult<ChairStatusResponse>>.Success(await chairs.GetPageAsync(query, ct));
    }
    public async Task<Result<ChairStatusResponse>> ChangeAsync(int id, int actorId, string? role, ChangeChairStatusRequest request, CancellationToken ct = default)
    {
        if (!RoleAllowed(role) || actorId <= 0) return Result<ChairStatusResponse>.Failure(Error.Forbidden);
        if (id <= 0 || !(await new ChangeChairStatusRequestValidator().ValidateAsync(request, ct)).IsValid)
            return Result<ChairStatusResponse>.Failure(Error.Validation);
        try
        {
            return await uow.ExecuteSerializableTransactionAsync(async transactionCt =>
            {
                if (!await chairs.ActorAllowedAsync(actorId, role!, transactionCt)) return Result<ChairStatusResponse>.Failure(Error.Forbidden);
                var chair = await chairs.FindAsync(id, transactionCt);
                if (chair is null) return Result<ChairStatusResponse>.Failure(Error.NotFound);
                if (chair.Version != request.ExpectedVersion || chair.Version == int.MaxValue) return Result<ChairStatusResponse>.Failure(Changed);
                // DL-163/317: never release a live visit, including inconsistent legacy chair flags.
                if (chair.Status == DentalChairStatus.Occupied || await chairs.HasActiveVisitAsync(id, transactionCt))
                    return Result<ChairStatusResponse>.Failure(Occupied);
                if (await chairs.IsAssignedAsync(id, transactionCt)) return Result<ChairStatusResponse>.Failure(Assigned);
                var active = request.Status != (int)DentalChairStatus.Inactive;
                if ((int)chair.Status == request.Status && chair.IsActive == active)
                    return Result<ChairStatusResponse>.Success(ChairStatusResponse.From(chair));
                var previousStatus = (int)chair.Status; var previousActive = chair.IsActive;
                chair.Status = (DentalChairStatus)request.Status; chair.IsActive = active;
                chair.Version++; chair.UpdatedAt = clock.UtcNow; chair.UpdatedByUserId = actorId;
                await uow.SaveChangesAsync(transactionCt);
                await audit.LogRequiredAsync("MOD_ASS_CHAIR_STATUS_CHANGED", "DentalChair", id, actorId,
                    JsonSerializer.Serialize(new { previousStatus, status = request.Status, previousActive, isActive = active, version = chair.Version }), ct: transactionCt);
                return Result<ChairStatusResponse>.Success(ChairStatusResponse.From(chair));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Result<ChairStatusResponse>.Failure(Changed); }
    }
}
