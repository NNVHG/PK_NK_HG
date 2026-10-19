using Dental.Application.Common;
using Dental.Application.Features.MOD_PAT;
using Dental.Application.Features.Patients.DTOs;

namespace Dental.Application.Interfaces;
public interface IQrPatientIntakeRepository
{
    Task<Result<QrIdentityLookupResult>> LookupAsync(int actorId, string role, QrIdentityLookupRequest request, CancellationToken ct);
    Task<Result<PatientResponse>> CreateAsync(int actorId, string role, QrPatientIntakeRequest request, string? ip, CancellationToken ct);
}
