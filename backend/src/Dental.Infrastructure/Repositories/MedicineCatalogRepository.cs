using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Repositories;
public sealed class MedicineCatalogRepository(DentalDbContext db) : IMedicineCatalogRepository
{
    public async Task<PagedResult<Medicine>> GetPageAsync(MedicineQuery request, CancellationToken ct)
    {
        var query = db.Medicines.AsNoTracking().Where(x => request.IncludeInactive || x.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        { var term = DiagnosisSearch.Normalize(request.Keyword); query = query.Where(x => x.SearchText.Contains(term)); }
        var count = await query.CountAsync(ct);
        return PagedResult<Medicine>.Create(await query.OrderBy(x => x.Code).ThenBy(x => x.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct), count, request.Page, request.PageSize);
    }
    public Task<Medicine?> FindAsync(int id, CancellationToken ct) => db.Medicines.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct) => db.Medicines.AnyAsync(x => x.Code == code && x.Id != exceptId, ct);
    public async Task AddAsync(Medicine medicine, CancellationToken ct) => await db.Medicines.AddAsync(medicine, ct);
}
