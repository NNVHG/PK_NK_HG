using Dental.Application.Features.Queue.DTOs;
using Dental.Domain.Enums;
using FluentValidation;

namespace Dental.Application.Features.Queue.Validators;

public sealed class UpdateQueueStatusRequestValidator : AbstractValidator<UpdateQueueStatusRequest>
{
    public UpdateQueueStatusRequestValidator()
    {
        RuleFor(x => x.NewStatus)
            .Must(status => Enum.IsDefined(typeof(QueueStatus), status))
            .WithMessage("Trạng thái mới không hợp lệ. Giá trị cho phép: 1 (Chờ khám), 2 (Đang khám), 3 (Khám xong), 4 (Đã hủy), 5 (Chụp X-quang).");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Lý do không được vượt quá 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Reason));
    }
}
