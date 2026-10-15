using Dental.Application.Features.Queue.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Queue.Validators;

public sealed class CheckInDebtRequestValidator : AbstractValidator<CheckInDebtRequest>
{
    public CheckInDebtRequestValidator()
    {
        RuleFor(request => request.PatientId).GreaterThan(0);
    }
}
