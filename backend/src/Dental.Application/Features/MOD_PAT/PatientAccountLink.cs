using Dental.Application.Common;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.MOD_PAT;

public sealed record FamilyAccountCandidate(int UserId, string FullName, string Phone, DateOnly? DateOfBirth);
public sealed record PatientAccountLinkContext(int PatientId, string PatientName, DateOnly DateOfBirth, string Phone,
    bool IsLinked, FamilyAccountCandidate? Account, string? ReviewStamp);
public sealed record LinkPatientAccountRequest(int AccountUserId, string ReviewStamp, bool Confirmed);
public sealed record PatientAccountLinked(int PatientId, int AccountUserId);
public interface IPatientAccountLinkRepository
{
    Task<Result<PatientAccountLinkContext>> ReadAsync(int patientId, int actor, string role, CancellationToken ct);
    Task<Result<PatientAccountLinked>> LinkAsync(int patientId, int actor, string role, LinkPatientAccountRequest request, string? ip, CancellationToken ct);
}
public sealed class LinkPatientAccountValidator : AbstractValidator<LinkPatientAccountRequest>
{
    public LinkPatientAccountValidator()
    {
        RuleFor(x => x.AccountUserId).GreaterThan(0);
        RuleFor(x => x.ReviewStamp).NotEmpty().Matches("^[A-F0-9]{64}$");
        RuleFor(x => x.Confirmed).Equal(true);
    }
}
public sealed class PatientAccountLinkService(IPatientAccountLinkRepository repository)
{
    private static bool Allowed(int actor, string? role) => actor > 0 && role is RoleCodes.Admin or RoleCodes.Receptionist;
    public Task<Result<PatientAccountLinkContext>> ReadAsync(int patient, int actor, string? role, CancellationToken ct = default)
        => !Allowed(actor, role) ? Task.FromResult(Result<PatientAccountLinkContext>.Failure(Error.Forbidden))
            : patient <= 0 ? Task.FromResult(Result<PatientAccountLinkContext>.Failure(Error.Validation))
            : repository.ReadAsync(patient, actor, role!, ct);
    public Task<Result<PatientAccountLinked>> LinkAsync(int patient, int actor, string? role, LinkPatientAccountRequest request, string? ip, CancellationToken ct = default)
        => !Allowed(actor, role) ? Task.FromResult(Result<PatientAccountLinked>.Failure(Error.Forbidden))
            : patient <= 0 || !new LinkPatientAccountValidator().Validate(request).IsValid
                ? Task.FromResult(Result<PatientAccountLinked>.Failure(Error.Validation))
                : repository.LinkAsync(patient, actor, role!, request, ip, ct);
}
