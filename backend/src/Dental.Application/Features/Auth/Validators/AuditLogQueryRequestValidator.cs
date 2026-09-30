using Dental.Application.Features.Auth.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Auth.Validators;

public sealed class AuditLogQueryRequestValidator : AbstractValidator<AuditLogQueryRequest>
{
    public AuditLogQueryRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("Số trang phải lớn hơn 0.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Số dòng mỗi trang phải từ 1 đến 100.");
        RuleFor(x => x.PerformedBy)
            .MaximumLength(100)
            .WithMessage("Người thực hiện không được vượt quá 100 ký tự.");
        RuleFor(x => x.Action)
            .MaximumLength(50)
            .WithMessage("Hành động không được vượt quá 50 ký tự.");
        RuleFor(x => x.EntityType)
            .MaximumLength(50)
            .WithMessage("Loại đối tượng không được vượt quá 50 ký tự.");
        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate <= x.ToDate)
            .WithMessage("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
    }
}
