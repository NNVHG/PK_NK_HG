using Dental.Application.Features.Queue.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Queue.Validators;

public sealed class QueueCleanupRequestValidator : AbstractValidator<QueueCleanupRequest>
{
    public QueueCleanupRequestValidator()
        => RuleFor(x => x.Confirmed).Equal(true).WithMessage("Cần xác nhận dọn các lượt chưa khám đã quá hạn.");
}
