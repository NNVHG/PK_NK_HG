using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IVisitRepository
{
    Task<Visit?> GetByIdAsync(int visitId, CancellationToken ct = default);
    Task<Visit?> GetForUpdateAsync(int visitId, CancellationToken ct = default);
    Task<bool> HasOpenVisitAsync(int patientId, CancellationToken ct = default);
    Task<bool> HasOtherOpenVisitAsync(int patientId, IReadOnlyCollection<int> excludedVisitIds, CancellationToken ct = default);
    Task<bool> HasActiveVisitForDentistAsync(int dentistId, CancellationToken ct = default, int? excludedVisitId = null);
    Task<bool> HasActiveVisitForChairAsync(int chairId, int excludedVisitId, CancellationToken ct = default);
    Task AddAsync(Visit visit, CancellationToken ct = default);
    Task<PagedResult<Visit>> GetPatientVisitsAsync(
        int patientId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<PagedResult<PatientTimelineItemResponse>> GetPatientTimelineAsync(
        int patientId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
