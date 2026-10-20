using Dental.Application.Features.MOD_ASS.DTOs;
using FluentValidation;

namespace Dental.Application.Features.MOD_ASS.Validators;

public sealed class ChairStatusQueryValidator : AbstractValidator<ChairStatusQuery>
{
    public ChairStatusQueryValidator()
    {
        RuleFor(x => x.Keyword).MaximumLength(100);
        RuleFor(x => x.Status).Must(s => s is null or 1 or 2 or 3 or 4);
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class ChangeChairStatusRequestValidator : AbstractValidator<ChangeChairStatusRequest>
{
    public ChangeChairStatusRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => s is 1 or 3 or 4);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
        RuleFor(x => x.Confirmed).Equal(true);
    }
}
