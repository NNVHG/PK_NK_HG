using Dental.Application.Features.MOD_MST.DTOs;
using FluentValidation;
namespace Dental.Application.Features.MOD_MST.Validators;
public sealed class DiagnosisQueryValidator : AbstractValidator<DiagnosisQuery>
{
    public DiagnosisQueryValidator()
    {
        RuleFor(x => x.Keyword).MaximumLength(200);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
public sealed class DiagnosisWriteValidator : AbstractValidator<DiagnosisWriteRequest>
{
    public DiagnosisWriteValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9._-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}
