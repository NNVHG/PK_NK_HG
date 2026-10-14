using Dental.Application.Features.MOD_BIL.DTOs;
using FluentValidation;
namespace Dental.Application.Features.MOD_BIL.Validators;
public sealed class InvoicePortalQueryValidator : AbstractValidator<InvoicePortalQuery>
{
    public InvoicePortalQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
