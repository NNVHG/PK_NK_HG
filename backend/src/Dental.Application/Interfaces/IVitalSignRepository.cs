using Dental.Application.Common;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IVitalSignRepository
{
    Task AddAsync(VitalSignRecord record, CancellationToken ct = default);
    Task<PagedResult<VitalSignRecord>> GetPatientRecordsAsync(
        int patientId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
