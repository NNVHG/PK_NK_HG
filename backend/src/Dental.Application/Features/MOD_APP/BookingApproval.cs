using Dental.Application.Common;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.MOD_APP;

public sealed record BookingApprovalPatient(int PatientId, string FullName, DateOnly DateOfBirth, bool CanSelect);
public sealed record BookingApprovalAccount(int UserId, string FullName);
public sealed record BookingApprovalResult(int AppointmentId, int PatientId, bool CreatedAccount, bool RequiresPhoneActivation);
public sealed record BookingApprovalContext(int Id, int Version, string Status, string FullName, string Phone,
    DateOnly DateOfBirth, string Gender, DateOnly AppointmentDate, TimeOnly SlotTime, int SlotDurationMinutes,
    string? DentistName, int? VerifiedPatientId, BookingApprovalAccount? Account, BookingApprovalPatient[] Patients,
    bool RequiresCounterConfirmation, string? BlockReason, string ReviewStamp, BookingApprovalResult? Result);
public sealed record ApproveBookingRequest(int ExpectedVersion, string ReviewStamp, int? PatientId,
    bool ConfirmedNewPatient, bool ConfirmedAtCounter, bool Confirmed);

public interface IBookingApprovalRepository
{
    Task<Result<BookingApprovalContext>> ReadAsync(int id, int actor, string role, CancellationToken ct);
    Task<Result<BookingApprovalResult>> ApproveAsync(int id, int actor, string role, ApproveBookingRequest request, string? ip, CancellationToken ct);
}
public sealed class ApproveBookingRequestValidator : AbstractValidator<ApproveBookingRequest>
{
    public ApproveBookingRequestValidator()
    {
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).LessThan(int.MaxValue);
        RuleFor(x => x.ReviewStamp).NotEmpty().Length(64).Matches("^[A-F0-9]{64}$");
        RuleFor(x => x.PatientId).GreaterThan(0).When(x => x.PatientId.HasValue);
        RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.ConfirmedNewPatient).Equal(true).When(x => !x.PatientId.HasValue);
        RuleFor(x => x.ConfirmedNewPatient).Equal(false).When(x => x.PatientId.HasValue);
    }
}
public sealed class BookingApprovalService(IBookingApprovalRepository repository)
{
    private static bool Allowed(int actor, string? role) => actor > 0 && role is RoleCodes.Admin or RoleCodes.Receptionist;
    public Task<Result<BookingApprovalContext>> ReadAsync(int id, int actor, string? role, CancellationToken ct = default)
        => !Allowed(actor, role) ? Task.FromResult(Result<BookingApprovalContext>.Failure(Error.Forbidden))
        : id < 1 ? Task.FromResult(Result<BookingApprovalContext>.Failure(Error.Validation))
        : repository.ReadAsync(id, actor, role!, ct);
    public Task<Result<BookingApprovalResult>> ApproveAsync(int id, int actor, string? role, ApproveBookingRequest request, string? ip, CancellationToken ct = default)
        => !Allowed(actor, role) ? Task.FromResult(Result<BookingApprovalResult>.Failure(Error.Forbidden))
        : id < 1 || !new ApproveBookingRequestValidator().Validate(request).IsValid
            ? Task.FromResult(Result<BookingApprovalResult>.Failure(Error.Validation))
            : repository.ApproveAsync(id, actor, role!, request, ip, ct);
}
