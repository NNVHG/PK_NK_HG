using Dental.Application.Features.ServiceCatalog.DTOs;
using FluentValidation;

namespace Dental.Application.Features.ServiceCatalog.Validators;

public sealed class ServiceIdRequestValidator : AbstractValidator<ServiceIdRequest>
{
    public ServiceIdRequestValidator()
    {
        RuleFor(request => request.Id)
            .GreaterThan(0).WithMessage("Mã dịch vụ không hợp lệ.");
    }
}
