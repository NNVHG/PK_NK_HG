using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Domain.Constants;
using FluentValidation;
namespace Dental.Application.Features.MOD_FDI.Validators;
public sealed class ToothHistoryRequestValidator : AbstractValidator<ToothHistoryRequest>
{
    public ToothHistoryRequestValidator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.ToothNumber).Must(x => x is null || FdiCodes.IsTooth(x.Value));
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
