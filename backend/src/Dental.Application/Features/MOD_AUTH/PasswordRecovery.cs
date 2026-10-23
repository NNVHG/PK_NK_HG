using Dental.Application.Common;
using Dental.Application.Interfaces;
using FluentValidation;

namespace Dental.Application.Features.MOD_AUTH;

public sealed record RecoveryRequest(string Phone, string? CitizenId = null);
public sealed record ResetPasswordRequest(Guid ChallengeId, string Phone, string Code, string NewPassword, string? CitizenId = null);
public sealed record RecoveryResponse(Guid ChallengeId, int ExpiresInSeconds = 300, int ResendAfterSeconds = 60);
public sealed record RecoveryAccount(int UserId, string PasswordHash, string Phone, string? CitizenIdHash);
public sealed record RecoveryCode(Guid ChallengeId, string Code);

public interface IPasswordRecoveryRepository
{
    Task<RecoveryAccount?> FindActiveAsync(string phone, string? citizenId, CancellationToken ct);
    Task<bool> ResetAsync(RecoveryAccount account, string passwordHash, CancellationToken ct);
}

public interface IPasswordRecoverySms
{
    bool IsConfigured { get; }
    Task<bool> SendAsync(string phone, string code, CancellationToken ct);
}

public interface IPasswordRecoveryDelivery
{
    bool IsConfigured { get; }
    bool Enqueue(string phone, RecoveryCode code);
}

public interface IPasswordRecoveryCodes
{
    Result<RecoveryCode> Issue(string phone, RecoveryAccount? account);
    RecoveryAccount? Consume(Guid challengeId, string phone, string code, string? citizenIdHash);
    void Revoke(string phone, Guid challengeId);
    bool IsCurrent(string phone, Guid challengeId);
}

public sealed class RecoveryRequestValidator : AbstractValidator<RecoveryRequest>
{
    public RecoveryRequestValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^0[0-9]{9}$");
        RuleFor(x => x.CitizenId).Must(x => string.IsNullOrEmpty(x) || System.Text.RegularExpressions.Regex.IsMatch(x, @"^[0-9]{12}$"));
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^0[0-9]{9}$");
        RuleFor(x => x.CitizenId).Must(x => string.IsNullOrEmpty(x) || System.Text.RegularExpressions.Regex.IsMatch(x, @"^[0-9]{12}$"));
        RuleFor(x => x.Code).NotEmpty().Matches(@"^[0-9]{6}$");
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(72)
            .Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]");
        RuleFor(x => x.NewPassword).Must(x => x is not null && !x.Contains('\0') && System.Text.Encoding.UTF8.GetByteCount(x) <= 72);
    }
}

public sealed class PasswordRecoveryService(IPasswordRecoveryRepository repository, IPasswordRecoveryCodes codes,
    IPasswordRecoveryDelivery sms, IPasswordHasher passwords)
{
    public static readonly Error Unavailable = new("AUTH_060", "Khôi phục mật khẩu qua SMS chưa sẵn sàng. Vui lòng liên hệ phòng khám.");
    public static readonly Error InvalidCode = new("AUTH_061", "Mã không đúng, đã hết hạn hoặc đã sử dụng. Vui lòng yêu cầu mã mới khi cần.");

    public async Task<Result<RecoveryResponse>> RequestAsync(RecoveryRequest request, CancellationToken ct = default)
    {
        if (!new RecoveryRequestValidator().Validate(request).IsValid)
            return Result<RecoveryResponse>.Failure(Error.Validation);
        // Configuration errors are reported before account lookup, with the same response for every phone.
        if (!sms.IsConfigured) return Result<RecoveryResponse>.Failure(Unavailable);
        var account = await repository.FindActiveAsync(request.Phone, request.CitizenId, ct);
        var issued = codes.Issue(request.Phone, account);
        if (issued.IsFailure) return Result<RecoveryResponse>.Failure(issued.Error);
        var code = issued.Value!;
        // Unknown/inactive accounts receive an indistinguishable challenge but never an SMS or reset grant.
        // Enqueue without awaiting external delivery: provider latency must not identify registered accounts.
        if (account is not null && !sms.Enqueue(request.Phone, code))
            codes.Revoke(request.Phone, code.ChallengeId);
        return Result<RecoveryResponse>.Success(new(code.ChallengeId));
    }

    public async Task<Result<bool>> ResetAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        if (!new ResetPasswordRequestValidator().Validate(request).IsValid)
            return Result<bool>.Failure(Error.Validation);
        var citizenIdHash = string.IsNullOrEmpty(request.CitizenId)
            ? null
            : Dental.Application.Features.MOD_PAT.CitizenIdIdentity.Hash(request.CitizenId);
        var account = codes.Consume(request.ChallengeId, request.Phone, request.Code, citizenIdHash);
        if (account is null) return Result<bool>.Failure(InvalidCode);
        // Consume before writing: concurrent replays can never reset twice, even if an audit/database write fails.
        return await repository.ResetAsync(account, passwords.Hash(request.NewPassword), ct)
            ? Result<bool>.Success(true) : Result<bool>.Failure(InvalidCode);
    }
}
