using Dental.Application.Features.ServiceCatalog.DTOs;
using FluentValidation;

namespace Dental.Application.Features.ServiceCatalog.Validators;

public sealed class CreateServicePriceRequestValidator : AbstractValidator<CreateServicePriceRequest>
{
    public CreateServicePriceRequestValidator()
    {
        RuleFor(request => request.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("Giá dịch vụ không được âm.");
    }
}
