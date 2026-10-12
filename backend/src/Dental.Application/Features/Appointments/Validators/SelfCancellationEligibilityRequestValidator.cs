using FluentValidation;
namespace Dental.Application.Features.Appointments.Validators;
public sealed class SelfCancellationEligibilityRequestValidator : AbstractValidator<int>
{
    public SelfCancellationEligibilityRequestValidator()
        => RuleFor(id => id).GreaterThan(0).OverridePropertyName("id").WithMessage("Mã lịch hẹn phải là số nguyên dương.");
}
