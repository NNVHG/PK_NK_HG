using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Domain.Constants;
namespace Dental.Application.Features.MOD_BIL.Services;
public sealed record RefundCalculation(decimal Total, decimal Discount, decimal Paid, decimal Refund,
    decimal DebtReduction, bool FullCancellation);
public static class InvoiceRefundRules
{
    public static Result<bool> RequiresLateApproval(IReadOnlyList<DateTime> receipts, DateTime now)
    {
        var late = receipts.Select(at => now - at > TimeSpan.FromDays(7)).Distinct().ToArray();
        // [CẦN XÁC NHẬN] Q-324: multiple receipts straddle the 7-day limit; do not choose first/latest without a decision.
        if (late.Length > 1) return Result<bool>.Failure(new("BIL_REFUND_WINDOW_UNRESOLVED", "Các lần thu nằm ở hai phía hạn 7 ngày. Chưa chốt mốc hoàn tiền cho hóa đơn thu nhiều lần (Q-324); chưa ghi nhận điều chỉnh."));
        return Result<bool>.Success(late.FirstOrDefault());
    }
    public static readonly string[] Reasons = ["CANCEL_BEFORE_TREATMENT", "MEDICAL_CONTRAINDICATION", "BILLING_ERROR", "PATIENT_REQUEST", "OTHER"];
    public static Result<RefundCalculation> Calculate(Invoice invoice, IReadOnlyList<RefundSelection> selection)
    {
        Result<RefundCalculation> Fail() => Result<RefundCalculation>.Failure(new("BIL_REFUND_STATE", "Hóa đơn hoặc các dòng hủy đã thay đổi; tải lại để đối chiếu."));
        if (invoice.Visit.Status != VisitStatuses.Completed || !invoice.Visit.IsLocked ||
            invoice.Status is not (InvoiceStatus.Paid or InvoiceStatus.PartiallyPaid or InvoiceStatus.PartiallyRefunded) ||
            invoice.RefundRevision == int.MaxValue || invoice.PaidAmount < 0 || invoice.PaidAmount > invoice.FinalAmount ||
            selection.Count is < 1 or > 200 || selection.Select(x=>x.InvoiceItemId).Distinct().Count()!=selection.Count)
            return Fail();
        var returned = invoice.Refunds.SelectMany(r=>r.Items).GroupBy(x=>x.InvoiceItemId).ToDictionary(g=>g.Key,g=>g.Sum(x=>(long)x.Quantity));
        var remaining = new Dictionary<int,int>();
        foreach (var item in invoice.Items)
        {
            var count = item.Quantity - returned.GetValueOrDefault(item.Id);
            if (item.Id<=0 || item.Quantity<=0 || count<0 || count>int.MaxValue || item.ItemType is not ("Medicine" or "Service") ||
                item.UnitPrice<0 || decimal.Truncate(item.UnitPrice)!=item.UnitPrice || item.TotalAmount!=item.UnitPrice*item.Quantity)
                return Fail();
            remaining[item.Id]=(int)count;
        }
        if (invoice.Items.Sum(i=>i.UnitPrice*remaining[i.Id])!=invoice.TotalAmount) return Fail();
        foreach (var line in selection)
        {
            if (!remaining.TryGetValue(line.InvoiceItemId,out var count) || line.Quantity<=0 || line.Quantity>count) return Fail();
            remaining[line.InvoiceItemId]-=line.Quantity;
        }
        var full=remaining.Values.All(x=>x==0);
        var total=invoice.Items.Sum(i=>i.UnitPrice*remaining[i.Id]);
        decimal discount;
        if (full || invoice.DiscountType is null && invoice.DiscountAmount==0) discount=0;
        else if (invoice.DiscountType is "Percentage" or "FixedAmount" && invoice.DiscountValue is decimal value && InvoiceDiscountRules.ValidValue(invoice.DiscountType,value))
            discount=invoice.DiscountType=="Percentage" ? Math.Round(invoice.Items.Where(i=>i.ItemType=="Service").Sum(i=>i.UnitPrice*remaining[i.Id])*value/100m,MidpointRounding.AwayFromZero) : value;
        else return Fail();
        if (discount>total) return Result<RefundCalculation>.Failure(new("BIL_REFUND_DISCOUNT","Mức giảm gốc vượt giá trị còn lại; không thể hoàn một phần theo DL-300."));
        var final=total-discount;
        if (final>invoice.FinalAmount) return Fail();
        var refund=Math.Max(0,invoice.PaidAmount-final); // DL-302: settle debt first.
        var paid=invoice.PaidAmount-refund;
        return Result<RefundCalculation>.Success(new(total,discount,paid,refund,
            invoice.FinalAmount-invoice.PaidAmount-(final-paid),full));
    }
}
