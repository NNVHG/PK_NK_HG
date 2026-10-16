using Dental.Application.Features.ServiceCatalog.DTOs;
using Dental.Application.Common;
using FluentValidation;

namespace Dental.Application.Features.ServiceCatalog.Validators;

public sealed class CreateDentalServiceRequestValidator : AbstractValidator<CreateDentalServiceRequest>
{
    public CreateDentalServiceRequestValidator(TimeProvider? timeProvider = null)
    {
        // DL-019: limits match the confirmed catalog schema.
        var clock = new VietnamClock(timeProvider ?? TimeProvider.System);
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
            .InclusiveBetween(0, 999999999999999999m).WithMessage("Giá dịch vụ vượt giới hạn lưu trữ.")
            .Must(value => decimal.Truncate(value) == value).WithMessage("Giá dịch vụ phải là số nguyên VND.");
        RuleFor(request => request.EffectiveFrom)
            .Must(value => value.UtcDateTime >= clock.Today.ToDateTime(TimeOnly.MinValue).AddHours(-7))
            .WithMessage("Ngày hiệu lực không được trước ngày hiện tại tại Việt Nam.");
    }
}
