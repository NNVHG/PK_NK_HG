using Dental.Application.Features.Queue.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Queue.Validators;

public sealed class CallNextRequestValidator : AbstractValidator<CallNextRequest>
{
    public CallNextRequestValidator()
    {
        When(x => x.DentistId.HasValue, () =>
        {
            RuleFor(x => x.DentistId!.Value)
                .GreaterThan(0)
                .WithMessage("DentistId phải là số nguyên dương.");
        });
    }
}
