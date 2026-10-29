using Dental.Application.Common;
using FluentValidation;

namespace Dental.Application.Features.MOD_AUTH;

public sealed record PatientPhonePreviewRequest(string NewPhone);
public sealed record PatientPhonePreview(int PatientId, string PatientName, string CurrentPhone, string NewPhone, bool ContactWillUpdate, string ReviewStamp);
public sealed record PatientPhoneChangeCommand(string NewPhone, string ReviewStamp, bool Confirmed, string CurrentPassword);
public sealed record PatientPhoneRequest(string NewPhone, string ReviewStamp, bool Confirmed);
public sealed record PatientPhoneConfirmation(Guid ChallengeId, string Code, string ReviewStamp, bool Confirmed);
public sealed record PatientPhonePending(Guid ChallengeId, string NewPhone, DateTimeOffset ExpiresAtUtc);
public sealed record PatientPhoneCompleted(bool Completed, bool PatientContactUpdated);
// Server-only binding, never returned in responses or logs.
public sealed record PatientPhoneTarget(int UserId, string NewPhone, string ReviewStamp);
public sealed record PatientPhoneDispatch(PatientPhonePending Pending, string Code);

public interface IPatientPhoneChangeRepository
{
    Task<Result<PatientPhonePreview>> PreviewAsync(int userId, string newPhone, CancellationToken ct);
    Task<Result<PatientPhoneCompleted>> ChangeAsync(int userId, PatientPhoneChangeCommand request, string? ip, CancellationToken ct);
}
public interface IPatientPhoneChangeProofs
{
    Result<PatientPhoneDispatch> Issue(PatientPhoneTarget target);
    PatientPhoneTarget? Consume(int userId, PatientPhoneConfirmation request);
    void Revoke(int userId, Guid challengeId);
}
public interface IPatientPhoneChangeSms
{
    bool IsConfigured { get; }
    Task<bool> SendAsync(string phone, string code, CancellationToken ct);
}
public sealed class PatientPhonePreviewValidator : AbstractValidator<PatientPhonePreviewRequest>
{
    public PatientPhonePreviewValidator() => RuleFor(x => x.NewPhone).NotEmpty().Length(10).Matches("^0[0-9]{9}$");
}
public sealed class PatientPhoneRequestValidator : AbstractValidator<PatientPhoneRequest>
{
    public PatientPhoneRequestValidator()
    {
        RuleFor(x => x.NewPhone).NotEmpty().Length(10).Matches("^0[0-9]{9}$");
        RuleFor(x => x.ReviewStamp).NotEmpty().Length(64).Matches("^[A-F0-9]{64}$");
        RuleFor(x => x.Confirmed).Equal(true);
    }
}
public sealed class PatientPhoneConfirmationValidator : AbstractValidator<PatientPhoneConfirmation>
{
    public PatientPhoneConfirmationValidator()
    {
        RuleFor(x => x.ChallengeId).NotEqual(Guid.Empty);
        RuleFor(x => x.Code).NotEmpty().Length(6).Matches("^[0-9]{6}$");
        RuleFor(x => x.ReviewStamp).NotEmpty().Length(64).Matches("^[A-F0-9]{64}$");
        RuleFor(x => x.Confirmed).Equal(true);
    }
}
public sealed class PatientPhoneChangeValidator : AbstractValidator<PatientPhoneChangeCommand>
{
    public PatientPhoneChangeValidator()
    {
        RuleFor(x => x.NewPhone).NotEmpty().Length(10).Matches("^0[0-9]{9}$");
        RuleFor(x => x.ReviewStamp).NotEmpty().Length(64).Matches("^[A-F0-9]{64}$");
        RuleFor(x => x.Confirmed).Equal(true);
        // Xác thực mật khẩu hiện tại, không áp quy tắc độ mạnh của mật khẩu mới.
        RuleFor(x => x.CurrentPassword).NotEmpty().Must(p => p is not null &&
            !p.Contains('\0') && System.Text.Encoding.UTF8.GetByteCount(p) <= 72);
    }
}
public sealed class PatientPhoneChangeService(IPatientPhoneChangeRepository repository, IPatientActivationDelivery delivery)
{
    public static readonly Error Invalid = new("PAT_PHONE_PROOF", "Yêu cầu không hợp lệ hoặc đã hết hạn.");
    public static readonly Error Stale = new("PAT_PHONE_STALE", "Tài khoản hoặc hồ sơ đã thay đổi. Vui lòng tải lại và xác nhận thông tin mới.");
    public static readonly Error NumberUnavailable = new("PAT_PHONE_NUMBER", "Không thể sử dụng số này. Vui lòng kiểm tra lại.");
    public static readonly Error Unavailable = new("PAT_PHONE_SMS_UNAVAILABLE", "Kênh xác minh OTP chưa sẵn sàng. Số và thông tin hồ sơ hiện tại được giữ nguyên.");
    public static readonly Error WrongPassword = new("PAT_PHONE_PASSWORD", "Mật khẩu hiện tại không đúng.");
    public Task<Result<PatientPhonePreview>> PreviewAsync(int userId, PatientPhonePreviewRequest request, CancellationToken ct)
        => new PatientPhonePreviewValidator().Validate(request).IsValid
            ? repository.PreviewAsync(userId, request.NewPhone, ct)
            : Task.FromResult(Result<PatientPhonePreview>.Failure(Error.Validation));
    public Task<Result<PatientPhoneCompleted>> ChangeAsync(int userId, PatientPhoneChangeCommand request, string? ip, CancellationToken ct)
    {
        if (!new PatientPhoneChangeValidator().Validate(request).IsValid)
            return Task.FromResult(Result<PatientPhoneCompleted>.Failure(Error.Validation));
        if (!delivery.IsConfigured) return Task.FromResult(Result<PatientPhoneCompleted>.Failure(Unavailable));
        return repository.ChangeAsync(userId, request, ip, ct);
    }
}
