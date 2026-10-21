using Dental.Application.Common;
using FluentValidation;

namespace Dental.Application.Features.Staff;

public sealed record StaffPhoneChangeRequest(string NewPhone, bool Confirmed);
public sealed record StaffPhoneChangeConfirmation(Guid ChallengeId, string Code, bool Confirmed);
public sealed record StaffPhoneChangePending(Guid ChallengeId, string NewPhone, DateTimeOffset ExpiresAtUtc);
// Server-only binding. Never serialize into an API response or a log.
public sealed record StaffPhoneChangeTarget(int ActorId, int UserId, string NewPhone, string ActorStamp, string UserStamp);
public sealed record StaffPhoneChangeDispatch(StaffPhoneChangePending Pending, string Code);

public interface IStaffPhoneChangeRepository
{
    Task<Result<StaffPhoneChangeTarget>> PrepareAsync(int actorId, int userId, string newPhone, string? ip, CancellationToken ct);
    Task<bool> CanReadAsync(int userId, CancellationToken ct);
    Task<Result<bool>> CompleteAsync(StaffPhoneChangeTarget target, string? ip, CancellationToken ct);
}
public interface IStaffPhoneChangeProofs
{
    Result<StaffPhoneChangeDispatch> Issue(StaffPhoneChangeTarget target);
    StaffPhoneChangePending? Pending(int userId);
    StaffPhoneChangeTarget? Consume(int userId, StaffPhoneChangeConfirmation request);
    void Revoke(int userId, Guid challengeId);
}
public interface IStaffPhoneChangeSms
{
    bool IsConfigured { get; }
    Task<bool> SendAsync(string phone, string code, CancellationToken ct);
}
public sealed class StaffPhoneChangeRequestValidator : AbstractValidator<StaffPhoneChangeRequest>
{
    public StaffPhoneChangeRequestValidator()
    {
        RuleFor(x => x.NewPhone).NotEmpty().Length(10).Matches("^0[0-9]{9}$");
        RuleFor(x => x.Confirmed).Equal(true);
    }
}
public sealed class StaffPhoneChangeConfirmationValidator : AbstractValidator<StaffPhoneChangeConfirmation>
{
    public StaffPhoneChangeConfirmationValidator()
    {
        RuleFor(x => x.ChallengeId).NotEqual(Guid.Empty);
        RuleFor(x => x.Code).NotEmpty().Length(6).Matches("^[0-9]{6}$");
        RuleFor(x => x.Confirmed).Equal(true);
    }
}
public sealed class StaffPhoneChangeService(IStaffPhoneChangeRepository repository, IStaffPhoneChangeProofs proofs, IStaffPhoneChangeSms sms)
{
    public static readonly Error Required = new("STAFF_PHONE_OTP_REQUIRED", "SĐT đã xác minh chỉ được đổi qua yêu cầu OTP số mới.");
    public static readonly Error Invalid = new("STAFF_PHONE_OTP_INVALID", "Yêu cầu hoặc mã không hợp lệ, đã dùng, hết hạn hoặc tài khoản đã thay đổi. Nhờ Admin gửi yêu cầu mới.");
    public static readonly Error Unavailable = new("STAFF_PHONE_SMS_UNAVAILABLE", "Kênh SMS đổi SĐT chưa sẵn sàng. Số đăng nhập hiện tại được giữ nguyên.");
    public async Task<Result<StaffPhoneChangePending>> RequestAsync(int actorId, int userId, StaffPhoneChangeRequest request, string? ip, CancellationToken ct)
    {
        if (!new StaffPhoneChangeRequestValidator().Validate(request).IsValid) return Result<StaffPhoneChangePending>.Failure(Error.Validation);
        // Authorization is checked even when the provider is unavailable.
        var target = await repository.PrepareAsync(actorId, userId, request.NewPhone, ip, ct);
        if (target.IsFailure) return Result<StaffPhoneChangePending>.Failure(target.Error);
        if (!sms.IsConfigured) return Result<StaffPhoneChangePending>.Failure(Unavailable);
        var issued = proofs.Issue(target.Value!);
        if (issued.IsFailure) return Result<StaffPhoneChangePending>.Failure(issued.Error);
        var dispatch = issued.Value!;
        try
        {
            if (await sms.SendAsync(request.NewPhone, dispatch.Code, ct)) return Result<StaffPhoneChangePending>.Success(dispatch.Pending);
        }
        catch (Exception) when (!ct.IsCancellationRequested) { /* Fail closed; never log transport credentials or the OTP. */ }
        proofs.Revoke(userId, dispatch.Pending.ChallengeId);
        return Result<StaffPhoneChangePending>.Failure(Unavailable);
    }
    public async Task<Result<StaffPhoneChangePending?>> PendingAsync(int userId, CancellationToken ct)
        => await repository.CanReadAsync(userId, ct)
            ? Result<StaffPhoneChangePending?>.Success(proofs.Pending(userId))
            : Result<StaffPhoneChangePending?>.Failure(Error.Forbidden);
    public async Task<Result<bool>> ConfirmAsync(int userId, StaffPhoneChangeConfirmation request, string? ip, CancellationToken ct)
    {
        if (!new StaffPhoneChangeConfirmationValidator().Validate(request).IsValid) return Result<bool>.Failure(Error.Validation);
        if (!await repository.CanReadAsync(userId, ct)) return Result<bool>.Failure(Error.Forbidden);
        var target = proofs.Consume(userId, request);
        return target is null ? Result<bool>.Failure(Invalid) : await repository.CompleteAsync(target, ip, ct);
    }
}
