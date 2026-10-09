using Dental.Application.Features.Appointments.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Appointments.Validators;

public sealed class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Lý do hủy không được vượt quá 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}
