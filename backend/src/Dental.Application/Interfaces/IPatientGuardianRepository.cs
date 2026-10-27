using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IPatientGuardianRepository
{
    Task<PatientGuardian?> GetActiveByWardPatientIdAsync(int wardPatientId, CancellationToken ct = default);
    Task<IReadOnlyList<PatientGuardian>> GetActiveByGuardianPatientIdAsync(int guardianPatientId, CancellationToken ct = default);
    Task<IReadOnlyList<PatientGuardian>> GetActiveByGuardianUserIdAsync(int guardianUserId, CancellationToken ct = default);
    Task<IReadOnlyList<PatientGuardian>> GetHistoryByWardPatientIdAsync(int wardPatientId, CancellationToken ct = default);
    Task<bool> HasActiveGuardianshipAsync(int guardianUserId, int wardPatientId, CancellationToken ct = default);
    Task<Result<PatientGuardianResponse>> AssignGuardianAsync(
        AssignPatientGuardianRequest request, int actorUserId, string? ipAddress, CancellationToken ct = default);
    Task<Result<PatientGuardianResponse>> RevokeGuardianAsync(
        int wardPatientId, int actorUserId, RevokePatientGuardianRequest request, string? ipAddress, CancellationToken ct = default);
    Task<Result<PatientResponse>> PromoteWardToIndependentPatientAsync(
        int wardPatientId, PromoteWardRequest request, int actorUserId, string? ipAddress, CancellationToken ct = default);
}
