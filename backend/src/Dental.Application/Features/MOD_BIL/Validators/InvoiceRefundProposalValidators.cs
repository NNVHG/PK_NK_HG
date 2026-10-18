using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using FluentValidation;

namespace Dental.Application.Features.MOD_BIL.Validators;

public sealed class InvoiceRefundProposalCreateValidator : AbstractValidator<InvoiceRefundProposalCreateRequest>
{
    public InvoiceRefundProposalCreateValidator()
    {
        RuleFor(x => x.Items).NotNull().Must(x => x is { Count: > 0 and <= 200 } && x.All(i => i is not null) && x.Select(i => i.InvoiceItemId).Distinct().Count() == x.Count);
        RuleForEach(x => x.Items).ChildRules(v => { v.RuleFor(x => x.InvoiceItemId).GreaterThan(0); v.RuleFor(x => x.Quantity).GreaterThan(0); });
        RuleFor(x => x.ReasonCode).Must(x => InvoiceRefundRules.Reasons.Contains(x));
        RuleFor(x => x.ReasonNotes).NotEmpty().Must(x => x is not null && x.Trim().Length is >= 5 and <= 500 && !x.Any(char.IsControl));
    }
}

public sealed class InvoiceRefundProposalPreviewValidator : AbstractValidator<InvoiceRefundProposalPreviewRequest>
{
    public InvoiceRefundProposalPreviewValidator() => RuleFor(x => x.PaymentMethod).Must(x => x is "Cash" or "BankTransfer");
}

public sealed class InvoiceRefundProposalDecisionValidator : AbstractValidator<InvoiceRefundProposalDecisionRequest>
{
    public InvoiceRefundProposalDecisionValidator()
    {
        RuleFor(x => x.PaymentMethod).Must(x => x is "Cash" or "BankTransfer");
        RuleFor(x => x.ExpectedPreview).NotEmpty().Matches("^[A-F0-9]{64}$");
        RuleFor(x => x.Confirmed).Equal(true);
    }
}

public sealed class InvoiceRefundProposalRejectValidator : AbstractValidator<InvoiceRefundProposalRejectRequest>
{
    public InvoiceRefundProposalRejectValidator() => RuleFor(x => x.DecisionNotes).Must(x => x is null || x.Length <= 500 && !x.Any(char.IsControl));
}