using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using FluentValidation;
namespace Dental.Application.Features.MOD_BIL.Validators;
public sealed class InvoiceRefundPreviewValidator : AbstractValidator<InvoiceRefundPreviewRequest>
{
    public InvoiceRefundPreviewValidator()
    {
        RuleFor(x=>x.Snapshot).NotEmpty().Matches("^[A-F0-9]{64}$");
        RuleFor(x=>x.PaymentMethod).Must(x=>x is "Cash" or "BankTransfer");
        RuleFor(x=>x.Items).NotNull().Must(x=>x is {Count:>0 and <=200} && x.All(i=>i is not null) && x.Select(i=>i.InvoiceItemId).Distinct().Count()==x.Count);
        RuleForEach(x=>x.Items).ChildRules(v=> {v.RuleFor(x=>x.InvoiceItemId).GreaterThan(0);v.RuleFor(x=>x.Quantity).GreaterThan(0);});
    }
}
public sealed class InvoiceRefundRequestValidator : AbstractValidator<InvoiceRefundRequest>
{
    public InvoiceRefundRequestValidator()
    {
        RuleFor(x=>x).Custom((x,c)=>{if(!new InvoiceRefundPreviewValidator().Validate(new InvoiceRefundPreviewRequest(x.Snapshot,x.Items,x.PaymentMethod)).IsValid)c.AddFailure("Items","Dữ liệu hủy không hợp lệ.");});
        RuleFor(x=>x.ExpectedPreview).NotEmpty().Matches("^[A-F0-9]{64}$");
        RuleFor(x=>x.RequestId).NotEmpty(); RuleFor(x=>x.Confirmed).Equal(true);
        RuleFor(x=>x.ReasonCode).Must(x=>InvoiceRefundRules.Reasons.Contains(x));
        RuleFor(x=>x.ReasonNotes).NotEmpty().Must(x=>x is not null && x.Trim().Length is >=5 and <=500 && !x.Any(char.IsControl));
    }
}
