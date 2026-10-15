using Dental.Application.Features.Queue.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Queue.Validators;

public sealed class CancelWaitingRequestValidator : AbstractValidator<CancelWaitingRequest>
{
    public CancelWaitingRequestValidator()
    {
        RuleFor(x => x.Confirmed).Equal(true).WithMessage("Cần xác nhận hủy lượt chờ chưa khám.");
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("Lý do không được vượt quá 500 ký tự.");
    }
}
