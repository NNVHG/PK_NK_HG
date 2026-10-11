using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.MOD_FDI.Validators;

public sealed class AssignServicesRequestValidator : AbstractValidator<AssignServicesRequest>
{
    public AssignServicesRequestValidator()
    {
        RuleFor(x => x.ServiceId).GreaterThan(0);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100);
        RuleFor(x => x.ToothNumbers).Must(x => x is null || x.Length <= 52 && x.Distinct().Count() == x.Length && x.All(FdiCodes.IsTooth))
            .WithMessage("Danh sách tối đa 52 răng FDI hợp lệ, không được trùng.");
        RuleFor(x => x.Surface).Must(x => x is null or "B" or "L" or "M" or "D" or "O")
            .WithMessage("Mặt răng phải là B/L/M/D/O hoặc null cho toàn răng.");
        RuleFor(x => x).Must(x => x.ToothNumbers is { Length: > 0 } || x.Surface is null)
            .WithMessage("Dịch vụ toàn hàm không được gắn mặt răng.");
    }
}
