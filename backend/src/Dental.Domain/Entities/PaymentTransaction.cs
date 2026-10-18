namespace Dental.Domain.Entities;

/// <summary>MOD_BIL: dữ liệu khoản thu chỉ thêm mới; DL-210 cho phép tăng metadata PrintCount. Hoàn tiền là dòng riêng.</summary>
public sealed class PaymentTransaction
{
    public int Id { get; set; }
    public string TransactionType { get; set; } = "Receipt";
    public string? RefundReasonCode { get; set; }
    public string? RefundReasonNotes { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public decimal? AmountTendered { get; set; }
    public decimal? ChangeAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public int CashierId { get; set; }
    public DateTime PaidAt { get; set; }
    public Guid RequestId { get; set; }
    public string? TransactionReference { get; set; }
    public string? Source { get; set; }
    public decimal? BankReceivedAmount { get; set; }
    public string? Note { get; set; }
    /// <summary>DL-210: print request metadata; financial fields remain immutable.</summary>
    public int PrintCount { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public User Cashier { get; set; } = null!;
}
