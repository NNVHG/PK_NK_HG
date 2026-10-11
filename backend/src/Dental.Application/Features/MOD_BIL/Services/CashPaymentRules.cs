using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.MOD_BIL.Services;

public static class CashPaymentRules
{
    public static bool IsSameRequest(PaymentTransaction payment, int invoiceId, int cashierId, CashPaymentRequest request)
        => payment.InvoiceId == invoiceId && payment.CashierId == cashierId && payment.Amount == request.Amount &&
            payment.AmountTendered == request.AmountTendered && payment.PaymentMethod == request.PaymentMethod;
    public static Result Apply(Invoice invoice, decimal amount)
    {
        if (invoice.Visit.Status != VisitStatuses.Completed || !invoice.Visit.IsLocked ||
            invoice.Status is not (InvoiceStatus.PendingPayment or InvoiceStatus.PartiallyPaid))
            return Result.Failure(new Error("BIL_031", "Chỉ thu tiền khi khám đã hoàn tất và hóa đơn còn nợ."));
        if (amount <= 0 || decimal.Truncate(amount) != amount || invoice.PaidAmount < 0 ||
            invoice.TotalAmount < invoice.PaidAmount || amount > invoice.TotalAmount - invoice.PaidAmount)
            return Result.Failure(new Error("BIL_032", "Số tiền thanh toán không được vượt quá số tiền còn nợ của hóa đơn."));
        invoice.PaidAmount += amount;
        invoice.Status = invoice.PaidAmount == invoice.TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        return Result.Success();
    }
}
