using Dental.Application.Features.Patients.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Patients.Validators;

public sealed class PatientQueryRequestValidator : AbstractValidator<PatientQueryRequest>
{
    public PatientQueryRequestValidator()
    {
        RuleFor(request => request.Page)
            .GreaterThan(0).WithMessage("Số trang phải lớn hơn 0.");
        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Số dòng mỗi trang phải từ 1 đến 50.");
        RuleFor(request => request.Keyword)
            .MaximumLength(100).WithMessage("Từ khóa tìm kiếm không được vượt quá 100 ký tự.");
    }
}
