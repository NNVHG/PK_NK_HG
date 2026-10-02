using Dental.Application.Common;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(int patientId, CancellationToken ct = default);
    Task<Patient?> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<PagedResult<Patient>> SearchAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<IReadOnlyList<Patient>> FindPossibleDuplicatesAsync(
        string phone,
        string fullName,
        DateOnly dateOfBirth,
        CancellationToken ct = default,
        int? excludePatientId = null);
    Task AddAsync(Patient patient, CancellationToken ct = default);
}
