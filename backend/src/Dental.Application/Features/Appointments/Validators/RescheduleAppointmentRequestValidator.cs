using Dental.Application.Features.Appointments.DTOs;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.Appointments.Validators;

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500)
            .WithMessage("Cần nhập lý do đổi lịch, tối đa 500 ký tự.");
        RuleFor(x => x.DentistId).GreaterThan(0).When(x => x.DentistId.HasValue);
        RuleFor(x => x.AppointmentDate)
            .NotEmpty()
            .WithMessage("Ngày hẹn không được để trống.");

        RuleFor(x => x.SlotTime)
            .Must(time => time.Ticks % TimeSpan.TicksPerMinute == 0)
            .WithMessage("Giờ hẹn phải chính xác đến phút; khung giờ được đối chiếu cấu hình hiện hành.");
        RuleFor(x => x.ExpectedScheduleRevision).Matches("^[A-F0-9]{64}$").When(x => x.ExpectedScheduleRevision is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
