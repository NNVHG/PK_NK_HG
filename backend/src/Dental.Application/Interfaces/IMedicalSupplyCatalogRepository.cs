using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IMedicalSupplyCatalogRepository
{
    Task<PagedResult<MedicalSupply>> GetPageAsync(MedicalSupplyQuery query, CancellationToken ct);
    Task<MedicalSupply?> FindAsync(int id, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct);
    Task AddAsync(MedicalSupply supply, CancellationToken ct);
}
