using Dental.Application.Features.Queue.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Queue.Validators;

public sealed class QueueAbsenceRequestValidator : AbstractValidator<QueueAbsenceRequest>
{
    public QueueAbsenceRequestValidator()
    {
        RuleFor(x => x.Confirmed).Equal(true).WithMessage("Cần xác nhận đúng bệnh nhân và thao tác hàng đợi.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
