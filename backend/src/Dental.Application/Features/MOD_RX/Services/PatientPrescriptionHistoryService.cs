using Dental.Application.Common;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Domain.Constants;
using FluentValidation;
namespace Dental.Application.Features.MOD_RX.Services;
public sealed class PatientPrescriptionQueryValidator : AbstractValidator<PatientPrescriptionQuery>
{
    public PatientPrescriptionQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
public sealed partial class PrescriptionService
{
    public Task<Result<PagedResult<PatientPrescriptionSummary>>> PatientHistoryAsync(int patientId, int actorId, string? role,
        PatientPrescriptionQuery query, CancellationToken ct = default)
    {
        if (!IsHistoryReader(actorId, role)) return Task.FromResult(Result<PagedResult<PatientPrescriptionSummary>>.Failure(Error.Forbidden));
        if (patientId <= 0 || !new PatientPrescriptionQueryValidator().Validate(query).IsValid)
            return Task.FromResult(Result<PagedResult<PatientPrescriptionSummary>>.Failure(Error.Validation));
        return repository.PatientHistoryAsync(patientId, actorId, role!, query, ct);
    }
    public Task<Result<PatientPrescriptionDetail>> PatientHistoryDetailAsync(int patientId, int prescriptionId, int actorId, string? role,
        CancellationToken ct = default)
    {
        if (!IsHistoryReader(actorId, role)) return Task.FromResult(Result<PatientPrescriptionDetail>.Failure(Error.Forbidden));
        if (patientId <= 0 || prescriptionId <= 0) return Task.FromResult(Result<PatientPrescriptionDetail>.Failure(Error.Validation));
        return repository.PatientHistoryDetailAsync(patientId, prescriptionId, actorId, role!, ct);
    }
    private static bool IsHistoryReader(int actorId, string? role) => actorId > 0 && role is
        RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Receptionist or RoleCodes.Assistant or RoleCodes.Patient;
}
