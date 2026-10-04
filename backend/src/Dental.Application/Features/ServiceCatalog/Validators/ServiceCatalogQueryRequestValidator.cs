using Dental.Application.Features.ServiceCatalog.DTOs;
using FluentValidation;

namespace Dental.Application.Features.ServiceCatalog.Validators;

public sealed class ServiceCatalogQueryRequestValidator : AbstractValidator<ServiceCatalogQueryRequest>
{
    public ServiceCatalogQueryRequestValidator()
    {
        RuleFor(request => request.Keyword)
            .MaximumLength(100).WithMessage("Từ khóa tìm kiếm không được vượt quá 100 ký tự.");
        RuleFor(request => request.Page)
            .GreaterThan(0).WithMessage("Số trang phải lớn hơn 0.");
        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Số dòng mỗi trang phải từ 1 đến 100.");
    }
}
