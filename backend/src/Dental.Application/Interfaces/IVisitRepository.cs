using Dental.Application.Common;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IVisitRepository
{
    Task<Visit?> GetByIdAsync(int visitId, CancellationToken ct = default);
    Task<bool> HasOpenVisitAsync(int patientId, CancellationToken ct = default);
    Task AddAsync(Visit visit, CancellationToken ct = default);
    Task<PagedResult<Visit>> GetPatientVisitsAsync(
        int patientId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
