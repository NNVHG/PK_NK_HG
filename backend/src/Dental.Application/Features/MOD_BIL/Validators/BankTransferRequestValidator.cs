using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using FluentValidation;

namespace Dental.Application.Features.MOD_BIL.Validators;

public sealed class BankTransferRequestValidator : AbstractValidator<BankTransferRequest>
{
    public BankTransferRequestValidator()
    {
        RuleFor(x => x.AddInfo).NotEmpty().MaximumLength(500).Must(x => BankTransferRules.InvoiceCode(x) is not null);
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(InvoiceServiceSnapshot.MaximumAmount)
            .Must(x => decimal.Truncate(x) == x);
        RuleFor(x => x.TransactionReference).NotEmpty().Matches("^[A-Za-z0-9_-]{6,20}$");
    }
}
