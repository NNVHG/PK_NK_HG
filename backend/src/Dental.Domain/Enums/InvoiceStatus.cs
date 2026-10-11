namespace Dental.Domain.Enums;

/// <summary>MOD_BIL: trạng thái chính thức theo DL-144.</summary>
public enum InvoiceStatus
{
    Draft = 0,
    PendingPayment = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Cancelled = 4
}
