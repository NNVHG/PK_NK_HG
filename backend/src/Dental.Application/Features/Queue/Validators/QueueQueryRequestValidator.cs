using Dental.Application.Features.Queue.DTOs;
using FluentValidation;
using Dental.Domain.Enums;

namespace Dental.Application.Features.Queue.Validators;

public sealed class QueueQueryRequestValidator : AbstractValidator<QueueQueryRequest>
{
    public QueueQueryRequestValidator()
    {
        RuleFor(x => x.Page)
            .InclusiveBetween(1, 1_000_000)
            .WithMessage("Trang phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Số lượng mỗi trang phải từ 1 đến 100.");
        RuleFor(x => x.Status).Must(status => !status.HasValue || Enum.IsDefined(typeof(QueueStatus), status.Value))
            .WithMessage("Trạng thái hàng đợi không hợp lệ.");
        RuleFor(x => x.DentistId).GreaterThan(0).When(x => x.DentistId.HasValue);
    }
}
