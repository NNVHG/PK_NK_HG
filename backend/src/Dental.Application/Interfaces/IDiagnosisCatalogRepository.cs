using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Domain.Entities;
namespace Dental.Application.Interfaces;
public interface IDiagnosisCatalogRepository
{
    Task<PagedResult<DentalDiagnosis>> GetPageAsync(DiagnosisQuery request, CancellationToken ct);
    Task<DentalDiagnosis?> FindAsync(int id, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct);
    Task AddAsync(DentalDiagnosis diagnosis, CancellationToken ct);
}
