using Dental.Application.Features.Appointments.DTOs;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.Appointments.Validators;

public sealed class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(x => x.DentistId).GreaterThan(0).When(x => x.DentistId.HasValue)
            .WithMessage("Mã nha sĩ phải là số nguyên dương.");
        RuleFor(x => x.PatientId)
            .GreaterThan(0)
            .WithMessage("Mã bệnh nhân không hợp lệ.");

        RuleFor(x => x.AppointmentDate)
            .NotEmpty()
            .WithMessage("Ngày hẹn không được để trống.");

        RuleFor(x => x.SlotTime)
            .Must(time => time.Ticks % TimeSpan.TicksPerMinute == 0)
            .WithMessage("Giờ hẹn phải chính xác đến phút; khung giờ được backend đối chiếu cấu hình hiện hành.");
        RuleFor(x => x.ExpectedScheduleRevision).Matches("^[A-F0-9]{64}$").When(x => x.ExpectedScheduleRevision is not null);

        // DL337: reject retired fields, including payloads from older UI versions.
        RuleFor(x => x.IsEmergency).Equal(false).WithMessage("Phòng khám không tiếp nhận cấp cứu.");
        RuleFor(x => x.EmergencyReason).Null().WithMessage("Ghi điều cần lưu ý vào ghi chú.");
        RuleFor(x => x.ConfirmedDentistConflictAppointmentId).Null().WithMessage("Không hỗ trợ chen lịch.");
        RuleFor(x => x.ConfirmedDentistConflictRequestId).Null().WithMessage("Không hỗ trợ chen lịch.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
