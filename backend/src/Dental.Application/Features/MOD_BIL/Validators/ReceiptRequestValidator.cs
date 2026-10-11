using Dental.Application.Features.MOD_BIL.DTOs;
using FluentValidation;
namespace Dental.Application.Features.MOD_BIL.Validators;
public sealed class ReceiptRequestValidator : AbstractValidator<ReceiptRequest>
{
    public ReceiptRequestValidator() { RuleFor(x => x.InvoiceId).GreaterThan(0); RuleFor(x => x.PaymentId).GreaterThan(0); }
}
