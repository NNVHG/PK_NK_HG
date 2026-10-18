using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class MedicalSupplyCatalogRepository(DentalDbContext db) : IMedicalSupplyCatalogRepository
{
    public async Task<PagedResult<MedicalSupply>> GetPageAsync(MedicalSupplyQuery query, CancellationToken ct)
    {
        var items = db.MedicalSupplies.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive);
        var key = DiagnosisSearch.Normalize(query.Keyword ?? string.Empty);
        if (key.Length > 0) items = items.Where(x => x.SearchText.Contains(key));
        var total = await items.CountAsync(ct);
        var list = await items.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return PagedResult<MedicalSupply>.Create(list, total, query.Page, query.PageSize);
    }

    public Task<MedicalSupply?> FindAsync(int id, CancellationToken ct) =>
        db.MedicalSupplies.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct) =>
        db.MedicalSupplies.AnyAsync(x => x.Code == code && (!exceptId.HasValue || x.Id != exceptId.Value), ct);

    public Task AddAsync(MedicalSupply supply, CancellationToken ct) =>
        db.MedicalSupplies.AddAsync(supply, ct).AsTask();
}
