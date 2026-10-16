using Dental.Application.Features.MOD_MST.DTOs;
using FluentValidation;
namespace Dental.Application.Features.MOD_MST.Validators;
public sealed class ChangeWorkspaceRequestValidator : AbstractValidator<ChangeWorkspaceRequest>
{
    public ChangeWorkspaceRequestValidator()
    {
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.ChairId).GreaterThan(0).When(x => x.ChairId.HasValue);
        RuleFor(x => x.DentistId).GreaterThan(0).When(x => x.DentistId.HasValue);
        RuleFor(x => x).Must(x => !(x.ChairId.HasValue && x.DentistId.HasValue));
    }
}
public sealed class WorkspaceTargetQueryValidator : AbstractValidator<WorkspaceTargetQuery>
{
    public WorkspaceTargetQueryValidator() { RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000); RuleFor(x => x.Keyword).MaximumLength(100); }
}
