using Dental.Application.Features.Appointments.DTOs;
using FluentValidation;
namespace Dental.Application.Features.Appointments.Validators;

public sealed class SimulateAppointmentRemindersValidator : AbstractValidator<SimulateAppointmentRemindersRequest>
{
    public SimulateAppointmentRemindersValidator() => RuleFor(x => x.Confirmed).Equal(true).WithMessage("Cần xác nhận mô phỏng nhắc hẹn.");
}
public sealed class ReminderLogQueryValidator : AbstractValidator<ReminderLogQueryRequest>
{
    public ReminderLogQueryValidator()
    {
        RuleFor(x => x.AppointmentId).GreaterThan(0).When(x => x.AppointmentId.HasValue);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
