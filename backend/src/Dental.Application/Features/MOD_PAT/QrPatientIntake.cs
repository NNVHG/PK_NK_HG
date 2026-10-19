using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.MOD_PAT;

public sealed record QrIdentityLookupRequest(string CitizenIdHash);
public sealed record QrExistingPatient(int PatientId, string PatientCode, string FullName, string? Masked, bool IsActive);
public sealed record QrIdentityLookupResult(QrExistingPatient? Existing);
public sealed record QrPatientIntakeRequest(string CitizenIdHash, string CitizenIdMasked, CreatePatientRequest Patient, bool Confirmed);
public sealed class QrIdentityLookupValidator : AbstractValidator<QrIdentityLookupRequest>
{
    public QrIdentityLookupValidator() => RuleFor(x => x.CitizenIdHash).NotEmpty().Length(64).Matches("^[A-Fa-f0-9]{64}$");
}
public sealed class QrPatientIntakeValidator : AbstractValidator<QrPatientIntakeRequest>
{
    public QrPatientIntakeValidator(VietnamClock clock)
    {
        RuleFor(x => x.CitizenIdHash).NotEmpty().Length(64).Matches("^[A-Fa-f0-9]{64}$");
        RuleFor(x => x.CitizenIdMasked).NotEmpty().Length(12).Matches(@"^[0-9]{3}\*{6}[0-9]{3}$");
        RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.Patient).NotNull().SetValidator(new CreatePatientRequestValidator(clock));
        When(x => x.Patient is not null, () => RuleFor(x => x.Patient.Phone).NotEmpty().Length(10).Matches("^0[0-9]{9}$"));
    }
}
public sealed class QrPatientIntakeService(IQrPatientIntakeRepository repository, VietnamClock clock)
{
    public Task<Result<QrIdentityLookupResult>> LookupAsync(int actorId, string? role, QrIdentityLookupRequest request, CancellationToken ct = default)
    {
        if (!Allowed(actorId, role)) return Task.FromResult(Result<QrIdentityLookupResult>.Failure(Error.Forbidden));
        if (!new QrIdentityLookupValidator().Validate(request).IsValid) return Task.FromResult(Result<QrIdentityLookupResult>.Failure(Error.Validation));
        return repository.LookupAsync(actorId, role!, request, ct);
    }
    public Task<Result<PatientResponse>> CreateAsync(int actorId, string? role, QrPatientIntakeRequest request, string? ip, CancellationToken ct = default)
    {
        if (!Allowed(actorId, role)) return Task.FromResult(Result<PatientResponse>.Failure(Error.Forbidden));
        if (!new QrPatientIntakeValidator(clock).Validate(request).IsValid) return Task.FromResult(Result<PatientResponse>.Failure(Error.Validation));
        return repository.CreateAsync(actorId, role!, request, ip, ct);
    }
    private static bool Allowed(int id, string? role) => id > 0 && role is RoleCodes.Admin or RoleCodes.Receptionist;
}
