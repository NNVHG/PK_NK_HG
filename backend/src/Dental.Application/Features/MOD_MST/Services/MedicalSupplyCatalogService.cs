using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.MOD_MST.Services;

public sealed class MedicalSupplyCatalogService(
    IMedicalSupplyCatalogRepository repo,
    IUnitOfWork uow,
    IAuditLogger audit,
    VietnamClock clock)
{
    public async Task<Result<PagedResult<MedicalSupplyResponse>>> GetAsync(string? role, MedicalSupplyQuery query, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin && (role != RoleCodes.Dentist || query.IncludeInactive))
            return Result<PagedResult<MedicalSupplyResponse>>.Failure(Error.Forbidden);
        var validation = new MedicalSupplyQueryValidator().Validate(query);
        if (!validation.IsValid)
            return Result<PagedResult<MedicalSupplyResponse>>.Failure(new Error("MST_090", validation.Errors[0].ErrorMessage));
        var page = await repo.GetPageAsync(query, ct);
        return Result<PagedResult<MedicalSupplyResponse>>.Success(
            PagedResult<MedicalSupplyResponse>.Create(page.Items.Select(Map).ToList(), page.TotalCount, page.Page, page.PageSize));
    }

    public async Task<Result<MedicalSupplyResponse>> SaveAsync(int? id, int? actorId, string? role, MedicalSupplyWriteRequest req, CancellationToken ct = default)
    {
        if (actorId is not > 0) return Result<MedicalSupplyResponse>.Failure(Error.Unauthorized);
        if (role != RoleCodes.Admin) return Result<MedicalSupplyResponse>.Failure(Error.Forbidden);
        if (id.HasValue && (id.Value <= 0 || req.ExpectedVersion <= 0))
            return Result<MedicalSupplyResponse>.Failure(new Error("MST_090", "Phiên bản vật tư không hợp lệ."));
        var cleaned = req with
        {
            Code = req.Code?.Trim().ToUpperInvariant() ?? string.Empty,
            Name = req.Name?.Trim() ?? string.Empty,
            Unit = req.Unit?.Trim() ?? string.Empty,
            Description = NullIfBlank(req.Description)
        };
        var validation = new MedicalSupplyWriteValidator().Validate(cleaned);
        if (!validation.IsValid)
            return Result<MedicalSupplyResponse>.Failure(new Error("MST_090", validation.Errors[0].ErrorMessage));
        try
        {
            return await uow.ExecuteSerializableTransactionAsync(async token =>
            {
                if (await repo.CodeExistsAsync(cleaned.Code, id, token))
                    return Result<MedicalSupplyResponse>.Failure(new Error("MST_091", "Mã vật tư đã tồn tại."));
                MedicalSupply item;
                string? oldValue = null;
                if (id.HasValue)
                {
                    var found = await repo.FindAsync(id.Value, token);
                    if (found is null) return Result<MedicalSupplyResponse>.Failure(Error.NotFound);
                    if (found.Version != cleaned.ExpectedVersion)
                        return Result<MedicalSupplyResponse>.Failure(new Error("MST_092", "Danh mục vật tư đã thay đổi. Vui lòng tải lại trước khi lưu."));
                    oldValue = Snapshot(found);
                    item = found;
                    item.Version++;
                    item.UpdatedAt = clock.UtcNow;
                    item.UpdatedByUserId = actorId.Value;
                }
                else
                {
                    item = new MedicalSupply
                    {
                        CreatedAt = clock.UtcNow,
                        CreatedByUserId = actorId.Value
                    };
                    await repo.AddAsync(item, token);
                }
                item.Code = cleaned.Code;
                item.Name = cleaned.Name;
                item.Unit = cleaned.Unit;
                item.MinimumStockThreshold = cleaned.MinimumStockThreshold;
                item.Description = cleaned.Description;
                item.IsActive = cleaned.IsActive;
                item.SearchText = DiagnosisSearch.Normalize($"{item.Code} {item.Name} {item.Unit}");
                await uow.SaveChangesAsync(token);
                await audit.LogRequiredAsync(
                    id.HasValue ? "MOD_MST_MEDICAL_SUPPLY_UPDATED" : "MOD_MST_MEDICAL_SUPPLY_CREATED",
                    "MedicalSupply",
                    item.Id,
                    actorId,
                    Snapshot(item),
                    oldValue,
                    token);
                return Result<MedicalSupplyResponse>.Success(Map(item));
            }, ct);
        }
        catch (ConcurrentOperationException)
        {
            return Result<MedicalSupplyResponse>.Failure(new Error("MST_092", "Danh mục vật tư vừa thay đổi đồng thời. Vui lòng tải lại."));
        }
    }

    public async Task<Result<MedicalSupplyResponse>> DeactivateAsync(int id, int expectedVersion, int? actorId, string? role, CancellationToken ct = default)
    {
        if (actorId is not > 0) return Result<MedicalSupplyResponse>.Failure(Error.Unauthorized);
        if (role != RoleCodes.Admin) return Result<MedicalSupplyResponse>.Failure(Error.Forbidden);
        if (id <= 0 || expectedVersion <= 0)
            return Result<MedicalSupplyResponse>.Failure(new Error("MST_090", "Phiên bản vật tư không hợp lệ."));
        try
        {
            return await uow.ExecuteSerializableTransactionAsync(async token =>
            {
                var item = await repo.FindAsync(id, token);
                if (item is null) return Result<MedicalSupplyResponse>.Failure(Error.NotFound);
                if (item.Version != expectedVersion)
                    return Result<MedicalSupplyResponse>.Failure(new Error("MST_092", "Danh mục vật tư đã thay đổi. Vui lòng tải lại trước khi ngừng sử dụng."));
                if (!item.IsActive) return Result<MedicalSupplyResponse>.Success(Map(item));
                var oldValue = Snapshot(item);
                item.IsActive = false;
                item.Version++;
                item.UpdatedAt = clock.UtcNow;
                item.UpdatedByUserId = actorId.Value;
                await uow.SaveChangesAsync(token);
                await audit.LogRequiredAsync("MOD_MST_MEDICAL_SUPPLY_DEACTIVATED", "MedicalSupply", item.Id, actorId, Snapshot(item), oldValue, token);
                return Result<MedicalSupplyResponse>.Success(Map(item));
            }, ct);
        }
        catch (ConcurrentOperationException)
        {
            return Result<MedicalSupplyResponse>.Failure(new Error("MST_092", "Danh mục vật tư vừa thay đổi đồng thời. Vui lòng tải lại."));
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Snapshot(MedicalSupply x) =>
        JsonSerializer.Serialize(new { x.Id, x.Code, x.Unit, x.MinimumStockThreshold, x.IsActive, x.Version });

    private static MedicalSupplyResponse Map(MedicalSupply x) =>
        new(x.Id, x.Code, x.Name, x.Unit, x.MinimumStockThreshold, x.Description, x.IsActive, x.Version, x.CreatedAt, x.UpdatedAt, x.CreatedByUserId, x.UpdatedByUserId);
}
