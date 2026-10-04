using Dental.Application.Features.ServiceCatalog.DTOs;
using FluentValidation;

namespace Dental.Application.Features.ServiceCatalog.Validators;

public sealed class UpdateDentalServiceRequestValidator : AbstractValidator<UpdateDentalServiceRequest>
{
    public UpdateDentalServiceRequestValidator()
    {
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Tên dịch vụ không được để trống.")
            .MaximumLength(200).WithMessage("Tên dịch vụ không được vượt quá 200 ký tự.");
        RuleFor(request => request.Description)
            .MaximumLength(1000).WithMessage("Mô tả dịch vụ không được vượt quá 1000 ký tự.");
        RuleFor(request => request.DurationMinutes)
            .GreaterThan(0).WithMessage("Thời lượng dịch vụ phải lớn hơn 0.");
    }
}
