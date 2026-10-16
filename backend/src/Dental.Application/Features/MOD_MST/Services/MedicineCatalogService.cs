using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
namespace Dental.Application.Features.MOD_MST.Services;
public sealed class MedicineCatalogService(IMedicineCatalogRepository medicines, IUnitOfWork uow, IAuditLogger audit, VietnamClock clock)
{
    public async Task<Result<PagedResult<MedicineResponse>>> GetAsync(string? role, MedicineQuery request, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin) return Result<PagedResult<MedicineResponse>>.Failure(Error.Forbidden);
        if (!(await new MedicineQueryValidator().ValidateAsync(request, ct)).IsValid) return Result<PagedResult<MedicineResponse>>.Failure(Error.Validation);
        var page = await medicines.GetPageAsync(request, ct);
        return Result<PagedResult<MedicineResponse>>.Success(PagedResult<MedicineResponse>.Create(page.Items.Select(ToResponse).ToList(), page.TotalCount, page.Page, page.PageSize));
    }
    public async Task<Result<MedicineResponse>> SaveAsync(int? id, int actorId, string? role, MedicineWriteRequest request, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin) return Result<MedicineResponse>.Failure(Error.Forbidden);
        request = request with { Code = request.Code?.Trim() ?? "", Name = request.Name?.Trim() ?? "", Unit = request.Unit?.Trim() ?? "" };
        if (actorId <= 0 || id <= 0 || !(await new MedicineWriteValidator().ValidateAsync(request, ct)).IsValid || id.HasValue && request.ExpectedVersion <= 0)
            return Result<MedicineResponse>.Failure(Error.Validation);
        try
        {
            return await uow.ExecuteSerializableTransactionAsync(async tct =>
            {
                var item = id.HasValue ? await medicines.FindAsync(id.Value, tct) : new Medicine { CreatedAt = clock.UtcNow };
                if (item is null) return Result<MedicineResponse>.Failure(Error.NotFound);
                if (id.HasValue && item.Version != request.ExpectedVersion) return Conflict();
                var code = request.Code.ToUpperInvariant();
                if (await medicines.CodeExistsAsync(code, id, tct)) return Result<MedicineResponse>.Failure("MST_081", "Mã thuốc đã tồn tại.");
                item.Code = code; item.Name = request.Name; item.ActiveIngredient = Optional(request.ActiveIngredient); item.Strength = Optional(request.Strength);
                item.Unit = request.Unit; item.UnitPrice = decimal.Truncate(request.UnitPrice); item.DefaultUsageInstruction = Optional(request.DefaultUsageInstruction); item.IsActive = request.IsActive;
                item.SearchText = DiagnosisSearch.Normalize(item.Code + " " + item.Name + " " + item.ActiveIngredient + " " + item.Strength);
                if (id.HasValue) { item.Version++; item.UpdatedAt = clock.UtcNow; } else await medicines.AddAsync(item, tct);
                await uow.SaveChangesAsync(tct);
                await audit.LogRequiredAsync(id.HasValue ? "MOD_MST_MEDICINE_UPDATED" : "MOD_MST_MEDICINE_CREATED", "Medicine", item.Id, actorId,
                    JsonSerializer.Serialize(new { item.Version, item.IsActive, item.UnitPrice, fields = new[] { "Code", "Name", "ActiveIngredient", "Strength", "Unit", "UnitPrice", "DefaultUsageInstruction", "IsActive" } }), ct: tct);
                return Result<MedicineResponse>.Success(ToResponse(item));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Conflict(); }
    }
    public async Task<Result<MedicineResponse>> DeactivateAsync(int id, int expectedVersion, int actorId, string? role, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin) return Result<MedicineResponse>.Failure(Error.Forbidden);
        if (id <= 0 || actorId <= 0 || expectedVersion <= 0) return Result<MedicineResponse>.Failure(Error.Validation);
        try
        {
            return await uow.ExecuteSerializableTransactionAsync(async tct =>
            {
                var item = await medicines.FindAsync(id, tct);
                if (item is null) return Result<MedicineResponse>.Failure(Error.NotFound);
                if (item.Version != expectedVersion) return Conflict();
                if (!item.IsActive) return Result<MedicineResponse>.Success(ToResponse(item));
                item.IsActive = false; item.Version++; item.UpdatedAt = clock.UtcNow;
                await uow.SaveChangesAsync(tct);
                await audit.LogRequiredAsync("MOD_MST_MEDICINE_DEACTIVATED", "Medicine", item.Id, actorId, JsonSerializer.Serialize(new { item.Version, item.IsActive }), ct: tct);
                return Result<MedicineResponse>.Success(ToResponse(item));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Conflict(); }
    }
    private static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    private static Result<MedicineResponse> Conflict() => Result<MedicineResponse>.Failure("MST_082", "Danh mục vừa thay đổi; tải lại để đối chiếu trước khi lưu.");
    private static MedicineResponse ToResponse(Medicine x) => new(x.Id, x.Code, x.Name, x.ActiveIngredient, x.Strength, x.Unit, x.UnitPrice, x.DefaultUsageInstruction, x.IsActive, x.Version, x.CreatedAt, x.UpdatedAt);
}
