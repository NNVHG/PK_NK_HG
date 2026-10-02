using Dental.Application.Features.MedicalHistory.DTOs;
using FluentValidation;

namespace Dental.Application.Features.MedicalHistory.Validators;

public sealed class MedicalHistoryQueryRequestValidator : AbstractValidator<MedicalHistoryQueryRequest>
{
    public MedicalHistoryQueryRequestValidator()
    {
        RuleFor(request => request.Page)
            .GreaterThan(0).WithMessage("Số trang phải lớn hơn 0.");

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Số dòng mỗi trang phải từ 1 đến 100.");
    }
}
