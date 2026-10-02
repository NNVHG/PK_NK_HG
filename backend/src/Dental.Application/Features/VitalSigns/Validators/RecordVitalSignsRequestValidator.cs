using Dental.Application.Features.VitalSigns.DTOs;
using FluentValidation;

namespace Dental.Application.Features.VitalSigns.Validators;

public sealed class RecordVitalSignsRequestValidator : AbstractValidator<RecordVitalSignsRequest>
{
    public RecordVitalSignsRequestValidator()
    {
        RuleFor(request => request)
            .Must(request => request.SystolicBp.HasValue ||
                request.DiastolicBp.HasValue ||
                request.PulseBpm.HasValue ||
                request.TemperatureC.HasValue)
            .WithMessage("Cần nhập ít nhất một chỉ số sinh hiệu.");

        RuleFor(request => request.SystolicBp)
            .InclusiveBetween(50, 260)
            .When(request => request.SystolicBp.HasValue)
            .WithMessage("Huyết áp tâm thu phải từ 50 đến 260 mmHg.");

        RuleFor(request => request.DiastolicBp)
            .InclusiveBetween(30, 160)
            .When(request => request.DiastolicBp.HasValue)
            .WithMessage("Huyết áp tâm trương phải từ 30 đến 160 mmHg.");

        RuleFor(request => request)
            .Must(request => !request.SystolicBp.HasValue ||
                !request.DiastolicBp.HasValue ||
                request.SystolicBp > request.DiastolicBp)
            .WithMessage("Huyết áp tâm thu phải lớn hơn huyết áp tâm trương.");

        RuleFor(request => request.PulseBpm)
            .InclusiveBetween(20, 220)
            .When(request => request.PulseBpm.HasValue)
            .WithMessage("Mạch phải từ 20 đến 220 lần/phút.");

        RuleFor(request => request.TemperatureC)
            .InclusiveBetween(30m, 43m)
            .When(request => request.TemperatureC.HasValue)
            .WithMessage("Thân nhiệt phải từ 30 đến 43 °C.");

        RuleFor(request => request.Note)
            .MaximumLength(2000).WithMessage("Ghi chú không được vượt quá 2000 ký tự.");
    }
}
