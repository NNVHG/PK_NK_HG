using Dental.Application.Features.Appointments.DTOs;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.Appointments.Validators;

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(x => x.AppointmentDate)
            .NotEmpty()
            .WithMessage("Ngày hẹn không được để trống.");

        RuleFor(x => x.SlotTime)
            .Must(AppointmentSlots.IsValidSlot)
            .WithMessage("Giờ hẹn không hợp lệ. Giờ khám: 08:00–11:30 và 13:30–16:30, slot 30 phút.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
