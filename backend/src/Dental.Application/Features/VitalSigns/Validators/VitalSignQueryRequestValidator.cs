using Dental.Application.Features.VitalSigns.DTOs;
using FluentValidation;

namespace Dental.Application.Features.VitalSigns.Validators;

public sealed class VitalSignQueryRequestValidator : AbstractValidator<VitalSignQueryRequest>
{
    public VitalSignQueryRequestValidator()
    {
        RuleFor(request => request.Page)
            .GreaterThan(0).WithMessage("Số trang phải lớn hơn 0.");

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Số dòng mỗi trang phải từ 1 đến 100.");
    }
}
