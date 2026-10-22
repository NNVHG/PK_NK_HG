using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using FluentValidation;

namespace Dental.Application.Features.MOD_BIL.Validators;

public sealed class ManualBankTransferRequestValidator : AbstractValidator<ManualBankTransferRequest>
{
    public ManualBankTransferRequestValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Confirmed).Equal(true);
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(InvoiceServiceSnapshot.MaximumAmount)
            .Must(x => decimal.Truncate(x) == x);
        RuleFor(x => x.ExpectedRemainingAmount).GreaterThan(0).LessThanOrEqualTo(InvoiceServiceSnapshot.MaximumAmount)
            .Must(x => decimal.Truncate(x) == x);
        RuleFor(x => x.TransactionReference).NotEmpty().Matches("^[A-Za-z0-9_-]{6,20}$");
    }
}
