using System.Text;
using Dental.Application.Common;
using Dental.Application.Interfaces;
using FluentValidation;

namespace Dental.Application.Features.MOD_AUTH;

public sealed record ActivationRequest(string Phone, string Password);
public sealed record ActivationVerification(Guid ChallengeId, string Code);
public sealed record ActivationCompletion(string CompletionToken, bool KeepDefaultPassword, string? NewPassword, bool Confirmed);
public sealed record ActivationChallenge(Guid ChallengeId, int ExpiresInSeconds = 300, int ResendAfterSeconds = 60, string? DevelopmentCode = null);
public sealed record ActivationGrant(string CompletionToken, int ExpiresInSeconds = 300, bool MustChangePassword = false);
public sealed record ActivationTarget(int UserId, string Stamp, bool MustChangePassword = false);
// Server-only credential context. Never returned from an API or written to logs.
public sealed record ActivationAccount(ActivationTarget Target, string PasswordHash, string Phone = "");
public sealed record ActivationDispatch(string Phone, Guid ChallengeId, string Code);

public interface IPatientActivationRepository
{
    Task<ActivationAccount?> FindPendingAsync(string phone, CancellationToken ct);
    Task<bool> IsCurrentAsync(ActivationTarget target, CancellationToken ct);
    Task<bool> CompleteAsync(ActivationTarget target, string? passwordHash, string? ip, CancellationToken ct);
}
public interface IPatientActivationProofs
{
    Result<ActivationDispatch> Issue(string phone, ActivationTarget? target);
    ActivationTarget? ConsumeCode(ActivationVerification request);
    Result<ActivationGrant> Grant(ActivationTarget target);
    ActivationTarget? ConsumeGrant(string token);
    bool IsCurrent(string phone, Guid challengeId);
    void Revoke(string phone, Guid challengeId);
}
public interface IPatientActivationDelivery
{
    bool IsConfigured { get; }
    bool ShowDevelopmentCode => false;
    bool Enqueue(ActivationDispatch dispatch);
}
public interface IPatientActivationSms
{
    bool IsConfigured { get; }
    Task<bool> SendAsync(string phone, string code, CancellationToken ct);
}
public sealed class ActivationRequestValidator : AbstractValidator<ActivationRequest>
{
    public ActivationRequestValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().Must(x => x is not null && System.Text.RegularExpressions.Regex.IsMatch(x, "^(0[0-9]{9}|[0-9]{12})$"));
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(72)
            .Must(x => x is not null && !x.Contains('\0') && Encoding.UTF8.GetByteCount(x) <= 72);
    }
}
public sealed class ActivationCompletionValidator : AbstractValidator<ActivationCompletion>
{
    public ActivationCompletionValidator()
    {
        RuleFor(x => x.CompletionToken).NotEmpty().Length(64).Matches("^[A-F0-9]{64}$");
        RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.NewPassword).Empty().When(x => x.KeepDefaultPassword);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(72)
            .Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]")
            .Must(x => x is not null && !x.Contains('\0') && Encoding.UTF8.GetByteCount(x) <= 72)
            .When(x => !x.KeepDefaultPassword);
    }
}
public sealed class PatientActivationService(IPatientActivationRepository repository, IPatientActivationProofs proofs,
    IPatientActivationDelivery delivery, IPasswordHasher passwords)
{
    public static readonly Error InvalidProof = new("AUTH_ACTIVATION_PROOF", "Mã hoặc phiên kích hoạt không hợp lệ, đã dùng hoặc hết hạn. Xin mã mới khi cần.");
    public static readonly Error Unavailable = new("AUTH_ACTIVATION_UNAVAILABLE", "Kênh OTP kích hoạt chưa sẵn sàng. Vui lòng liên hệ phòng khám.");
    public static readonly Error RegistrationDisabled = new("AUTH_REGISTRATION_DISABLED", "Vui lòng gửi yêu cầu đặt lịch. Tài khoản mới được tạo sau khi Lễ tân xác nhận lịch.");

    public async Task<Result<ActivationChallenge>> RequestAsync(ActivationRequest request, CancellationToken ct = default)
    {
        if (!new ActivationRequestValidator().Validate(request).IsValid) return Result<ActivationChallenge>.Failure(Error.Validation);
        if (!delivery.IsConfigured) return Result<ActivationChallenge>.Failure(Unavailable);
        var account = await repository.FindPendingAsync(request.Phone, ct);
        // Equal password work for missing/old/locked accounts; no provider timing or account status in the response.
        var padding = passwords.Hash(Guid.NewGuid().ToString("N"));
        var validPassword = passwords.Verify(request.Password, account?.PasswordHash ?? padding);
        var phone = account?.Phone ?? request.Phone;
        var issued = proofs.Issue(phone, validPassword ? account?.Target : null);
        if (issued.IsFailure) return Result<ActivationChallenge>.Failure(issued.Error);
        var dispatch = issued.Value!;
        if (account is not null && validPassword && !delivery.Enqueue(dispatch)) proofs.Revoke(phone, dispatch.ChallengeId);
        return Result<ActivationChallenge>.Success(new(dispatch.ChallengeId, DevelopmentCode: delivery.ShowDevelopmentCode && account is not null && validPassword ? dispatch.Code : null));
    }
    public async Task<Result<ActivationGrant>> VerifyAsync(ActivationVerification request, CancellationToken ct = default)
    {
        if (request.ChallengeId == Guid.Empty || request.Code is null || request.Code.Length != 6 ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.Code, "^[0-9]{6}$")) return Result<ActivationGrant>.Failure(Error.Validation);
        var target = proofs.ConsumeCode(request);
        if (target is null || !await repository.IsCurrentAsync(target, ct)) return Result<ActivationGrant>.Failure(InvalidProof);
        var grant = proofs.Grant(target);
        return grant.IsSuccess ? Result<ActivationGrant>.Success(grant.Value! with { MustChangePassword = target.MustChangePassword }) : grant;
    }
    public async Task<Result<bool>> CompleteAsync(ActivationCompletion request, string? ip, CancellationToken ct = default)
    {
        if (!new ActivationCompletionValidator().Validate(request).IsValid) return Result<bool>.Failure(Error.Validation);
        var target = proofs.ConsumeGrant(request.CompletionToken);
        if (target is null) return Result<bool>.Failure(InvalidProof);
        if (target.MustChangePassword && request.KeepDefaultPassword) return Result<bool>.Failure(Error.Validation);
        var hash = request.KeepDefaultPassword ? null : passwords.Hash(request.NewPassword!);
        return await repository.CompleteAsync(target, hash, ip, ct) ? Result<bool>.Success(true) : Result<bool>.Failure(InvalidProof);
    }
}
