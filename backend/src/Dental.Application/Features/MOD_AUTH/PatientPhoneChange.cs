using Dental.Application.Common;
using FluentValidation;

namespace Dental.Application.Features.MOD_AUTH;

public sealed record PatientPhonePreviewRequest(string NewPhone);
public sealed record PatientPhoneScopeItem(int PatientId, string FullName, DateOnly DateOfBirth, bool IsActive, bool WillUpdate);
public sealed record PatientPhonePreview(string CurrentPhone, string NewPhone, string ReviewStamp, IReadOnlyList<PatientPhoneScopeItem> Profiles);
public sealed record PatientPhoneRequest(string NewPhone, string ReviewStamp, bool Confirmed);
public sealed record PatientPhoneConfirmation(Guid ChallengeId, string Code, string ReviewStamp, bool Confirmed);
public sealed record PatientPhonePending(Guid ChallengeId, string NewPhone, DateTimeOffset ExpiresAtUtc);
public sealed record PatientPhoneCompleted(bool Completed, int UpdatedProfiles);
// Server-only binding, never returned in responses or logs.
public sealed record PatientPhoneTarget(int UserId, string NewPhone, string ReviewStamp);
public sealed record PatientPhoneDispatch(PatientPhonePending Pending, string Code);

public interface IPatientPhoneChangeRepository
{
    Task<Result<PatientPhonePreview>> PreviewAsync(int userId, string newPhone, CancellationToken ct);
    Task<Result<PatientPhoneTarget>> PrepareAsync(int userId, PatientPhoneRequest request, string? ip, CancellationToken ct);
    Task<Result<PatientPhoneCompleted>> CompleteAsync(PatientPhoneTarget target, string? ip, CancellationToken ct);
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
public sealed class PatientPhoneChangeService(IPatientPhoneChangeRepository repository, IPatientPhoneChangeProofs proofs, IPatientPhoneChangeSms sms)
{
    public static readonly Error Invalid = new("PAT_PHONE_PROOF", "Mã hoặc yêu cầu không hợp lệ, đã dùng hoặc hết hạn. Xem lại danh sách và xin OTP mới.");
    public static readonly Error Stale = new("PAT_PHONE_STALE", "Tài khoản hoặc danh sách hồ sơ đã thay đổi. Vui lòng xem lại và xác nhận phạm vi mới.");
    public static readonly Error NumberUnavailable = new("PAT_PHONE_NUMBER", "Không thể sử dụng số này để đổi đăng nhập. Vui lòng kiểm tra lại.");
    public static readonly Error Unavailable = new("PAT_PHONE_SMS_UNAVAILABLE", "Kênh SMS đổi SĐT chưa sẵn sàng. Số và thông tin hồ sơ hiện tại được giữ nguyên.");
    public Task<Result<PatientPhonePreview>> PreviewAsync(int userId, PatientPhonePreviewRequest request, CancellationToken ct)
        => new PatientPhonePreviewValidator().Validate(request).IsValid
            ? repository.PreviewAsync(userId, request.NewPhone, ct)
            : Task.FromResult(Result<PatientPhonePreview>.Failure(Error.Validation));
    public async Task<Result<PatientPhonePending>> RequestAsync(int userId, PatientPhoneRequest request, string? ip, CancellationToken ct)
    {
        if (!new PatientPhoneRequestValidator().Validate(request).IsValid) return Result<PatientPhonePending>.Failure(Error.Validation);
        var target = await repository.PrepareAsync(userId, request, ip, ct);
        if (target.IsFailure) return Result<PatientPhonePending>.Failure(target.Error);
        if (!sms.IsConfigured) return Result<PatientPhonePending>.Failure(Unavailable);
        var result = proofs.Issue(target.Value!);
        if (result.IsFailure) return Result<PatientPhonePending>.Failure(result.Error);
        var dispatch = result.Value!;
        var delivered = false;
        try
        {
            delivered = await sms.SendAsync(request.NewPhone, dispatch.Code, ct);
            return delivered ? Result<PatientPhonePending>.Success(dispatch.Pending) : Result<PatientPhonePending>.Failure(Unavailable);
        }
        catch (Exception) when (!ct.IsCancellationRequested) { return Result<PatientPhonePending>.Failure(Unavailable); }
        finally { if (!delivered) proofs.Revoke(userId, dispatch.Pending.ChallengeId); }
    }
    public Task<Result<PatientPhoneCompleted>> ConfirmAsync(int userId, PatientPhoneConfirmation request, string? ip, CancellationToken ct)
    {
        if (!new PatientPhoneConfirmationValidator().Validate(request).IsValid) return Task.FromResult(Result<PatientPhoneCompleted>.Failure(Error.Validation));
        var target = proofs.Consume(userId, request);
        return target is null ? Task.FromResult(Result<PatientPhoneCompleted>.Failure(Invalid)) : repository.CompleteAsync(target, ip, ct);
    }
}
