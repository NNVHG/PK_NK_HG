using System.Text.RegularExpressions;
using Dental.Application.Common;
using Dental.Application.Features.MOD_PAT;
using Dental.Application.Interfaces;
using FluentValidation;

namespace Dental.Application.Features.MOD_RX;
public sealed record PublicPrescriptionSummary(string PrescriptionCode, DateTime CreatedAt);
public sealed record PublicPrescriptionVerifyRequest(string CitizenId);
public sealed record PublicPrescriptionSession(string Token, DateTimeOffset ExpiresAt);
public sealed record PublicPrescriptionProof(int Id, int CitizenIdVersion, string? CitizenIdHash);
public sealed record PublicPrescriptionMedicine(string Name, string? Strength, string Unit, int Quantity, string UsageInstructions, int? TreatmentDays);
public sealed record PublicPrescriptionDetails(string PrescriptionCode, DateTime CreatedAt, string PatientName, string DoctorName,
    string Diagnosis, string? DoctorAdvice, DateOnly? RevisitDate, IReadOnlyList<PublicPrescriptionMedicine> Items);
public sealed class PublicPrescriptionVerifyValidator : AbstractValidator<PublicPrescriptionVerifyRequest>
{
    public PublicPrescriptionVerifyValidator() => RuleFor(x => x.CitizenId).NotEmpty().Length(12).Matches("^[0-9]{12}$");
}
public sealed class PublicPrescriptionService(IPublicPrescriptionRepository repository, IPublicPrescriptionAccess access)
{
    public static bool ValidCode(string? code) => code is { Length: 16 } && Regex.IsMatch(code, "^RX-[0-9]{8}-[0-9]{4}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static readonly Error Locked = new("RX_PUBLIC_LOCKED", "Đã vượt giới hạn đối chiếu. Vui lòng thử lại sau 15 phút.");
    public async Task<Result<PublicPrescriptionSummary>> SummaryAsync(string code, CancellationToken ct = default)
    {
        if (!ValidCode(code)) return Result<PublicPrescriptionSummary>.Failure(Error.Validation);
        var result = await repository.SummaryAsync(code, ct);
        return result is null ? Result<PublicPrescriptionSummary>.Failure(Error.NotFound) : Result<PublicPrescriptionSummary>.Success(result);
    }
    public async Task<Result<PublicPrescriptionSession>> VerifyAsync(string code, PublicPrescriptionVerifyRequest input, CancellationToken ct = default)
    {
        if (!ValidCode(code) || !new PublicPrescriptionVerifyValidator().Validate(input).IsValid)
            return Result<PublicPrescriptionSession>.Failure(Error.Validation);
        if (access.IsLocked(code)) return Result<PublicPrescriptionSession>.Failure(Locked);
        var proof = await repository.ProofAsync(code, ct);
        if (proof is null) return Result<PublicPrescriptionSession>.Failure(Error.Forbidden);
        var matches = CitizenIdIdentity.Matches(proof.CitizenIdHash, input.CitizenId);
        if (!access.RecordResult(code, matches)) return Result<PublicPrescriptionSession>.Failure(Locked);
        if (!matches) return Result<PublicPrescriptionSession>.Failure(Error.Forbidden);
        return Result<PublicPrescriptionSession>.Success(access.Issue(code, proof!.Id, proof.CitizenIdVersion));
    }
    public async Task<Result<PublicPrescriptionDetails>> DetailsAsync(string code, string? token, CancellationToken ct = default)
    {
        if (!ValidCode(code)) return Result<PublicPrescriptionDetails>.Failure(Error.Validation);
        if (string.IsNullOrEmpty(token) || token.Length > 4096) return Result<PublicPrescriptionDetails>.Failure(Error.Forbidden);
        var proof = await repository.ProofAsync(code, ct);
        if (proof is null || !access.Validate(code, proof.Id, proof.CitizenIdVersion, token))
            return Result<PublicPrescriptionDetails>.Failure(Error.Forbidden);
        var details = await repository.DetailsAsync(code, proof.CitizenIdVersion, ct);
        return details is null ? Result<PublicPrescriptionDetails>.Failure(Error.Forbidden) : Result<PublicPrescriptionDetails>.Success(details);
    }
}
