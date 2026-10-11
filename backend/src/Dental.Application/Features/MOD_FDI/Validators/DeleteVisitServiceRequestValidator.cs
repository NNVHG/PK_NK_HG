using Dental.Application.Features.MOD_FDI.DTOs;
using FluentValidation;
namespace Dental.Application.Features.MOD_FDI.Validators;
public sealed class DeleteVisitServiceRequestValidator : AbstractValidator<DeleteVisitServiceRequest>
{
    public DeleteVisitServiceRequestValidator()
    {
        RuleFor(x => x.VisitId).GreaterThan(0);
        RuleFor(x => x.AssignmentId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
