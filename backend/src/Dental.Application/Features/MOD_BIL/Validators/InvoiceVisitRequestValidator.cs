using FluentValidation;

namespace Dental.Application.Features.MOD_BIL.Validators;

public sealed record InvoiceVisitRequest(int VisitId);
public sealed class InvoiceVisitRequestValidator : AbstractValidator<InvoiceVisitRequest>
{
    public InvoiceVisitRequestValidator() => RuleFor(x => x.VisitId).GreaterThan(0);
}
