using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using FluentValidation;

namespace Dental.Application.Features.MOD_BIL.Validators;

public sealed class CashPaymentRequestValidator : AbstractValidator<CashPaymentRequest>
{
    public CashPaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(InvoiceServiceSnapshot.MaximumAmount)
            .Must(x => decimal.Truncate(x) == x);
        RuleFor(x => x.AmountTendered).GreaterThanOrEqualTo(x => x.Amount)
            .LessThanOrEqualTo(InvoiceServiceSnapshot.MaximumAmount).Must(x => decimal.Truncate(x) == x);
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.PaymentMethod).Equal("Cash").WithMessage("Lượt này chỉ hỗ trợ thu tiền mặt.");
    }
}
