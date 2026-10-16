using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Domain.Entities;
namespace Dental.Application.Interfaces;
public interface IMedicineCatalogRepository
{
    Task<PagedResult<Medicine>> GetPageAsync(MedicineQuery query, CancellationToken ct);
    Task<Medicine?> FindAsync(int id, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct);
    Task AddAsync(Medicine medicine, CancellationToken ct);
}
