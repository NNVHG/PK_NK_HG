using Dental.Application.Features.ServiceCatalog.DTOs;
using FluentValidation;

namespace Dental.Application.Features.ServiceCatalog.Validators;

public sealed class CreateDentalServiceRequestValidator : AbstractValidator<CreateDentalServiceRequest>
{
    public CreateDentalServiceRequestValidator()
    {
        // [CẦN XÁC NHẬN] Độ dài Code/Name/Description đang theo giới hạn cấu hình DB tạm thời.
        RuleFor(request => request.Code)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Mã dịch vụ không được để trống.")
            .MaximumLength(50).WithMessage("Mã dịch vụ không được vượt quá 50 ký tự.");
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Tên dịch vụ không được để trống.")
            .MaximumLength(200).WithMessage("Tên dịch vụ không được vượt quá 200 ký tự.");
        RuleFor(request => request.Description)
            .MaximumLength(1000).WithMessage("Mô tả dịch vụ không được vượt quá 1000 ký tự.");
        RuleFor(request => request.DurationMinutes)
            .GreaterThan(0).WithMessage("Thời lượng dịch vụ phải lớn hơn 0.");
        RuleFor(request => request.InitialPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Giá dịch vụ không được âm.");
    }
}
