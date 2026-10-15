using Dental.Application.Features.ServiceCatalog.DTOs;
using Dental.Application.Common;
using FluentValidation;

namespace Dental.Application.Features.ServiceCatalog.Validators;

public sealed class CreateServicePriceRequestValidator : AbstractValidator<CreateServicePriceRequest>
{
    public CreateServicePriceRequestValidator(TimeProvider? timeProvider = null)
    {
        var clock = new VietnamClock(timeProvider ?? TimeProvider.System);
        RuleFor(request => request.Amount)
            .InclusiveBetween(0, 999999999999999999m).WithMessage("Giá dịch vụ vượt giới hạn lưu trữ.")
            .Must(value => decimal.Truncate(value) == value).WithMessage("Giá dịch vụ phải là số nguyên VND.");
        RuleFor(request => request.EffectiveFrom)
            .Must(value => value.UtcDateTime >= clock.Today.ToDateTime(TimeOnly.MinValue).AddHours(-7))
            .WithMessage("Ngày hiệu lực không được trước ngày hiện tại tại Việt Nam.");
    }
}
