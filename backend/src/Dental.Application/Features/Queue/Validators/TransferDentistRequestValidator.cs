using Dental.Application.Features.Queue.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Queue.Validators;

public sealed class TransferDentistRequestValidator : AbstractValidator<TransferDentistRequest>
{
    public TransferDentistRequestValidator()
    {
        RuleFor(x => x.TargetDentistId).GreaterThan(0);
        RuleFor(x => x.ExpectedDentistId).GreaterThan(0).NotEqual(x => x.TargetDentistId);
        RuleFor(x => x.ExpectedStatus).Must(status => status is 1 or 2 or 5 or 6);
        RuleFor(x => x.Confirmed).Equal(true).WithMessage("Cần xác nhận đúng lượt và nha sĩ tiếp nhận.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
