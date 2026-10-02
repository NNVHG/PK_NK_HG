using Dental.Application.Common;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IMedicalHistoryRepository
{
    Task AddAsync(MedicalHistoryRecord record, CancellationToken ct = default);
    Task<MedicalHistoryRecord?> GetLatestByPatientIdAsync(int patientId, CancellationToken ct = default);
    Task<PagedResult<MedicalHistoryRecord>> GetPatientHistoryAsync(
        int patientId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
