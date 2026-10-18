namespace Dental.Domain.Entities;
/// <summary>MOD_BIL: append-only adjustment of original billed quantities; Q-323/DL-302.</summary>
public sealed class InvoiceRefund
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int Revision { get; set; }
    public Guid RequestId { get; set; }
    public string PayloadHash { get; set; } = "";
    public int ActorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? PaymentTransactionId { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal DebtReduction { get; set; }
    public decimal TotalBefore { get; set; }
    public decimal DiscountBefore { get; set; }
    public decimal PaidBefore { get; set; }
    public decimal TotalAfter { get; set; }
    public decimal DiscountAfter { get; set; }
    public decimal PaidAfter { get; set; }
    public string ReasonCode { get; set; } = "";
    public string ReasonNotes { get; set; } = "";
    public string PaymentMethod { get; set; } = "Cash";
    public bool LateApproval { get; set; }
    public bool ReserveConfirmed { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public User Actor { get; set; } = null!;
    public PaymentTransaction? PaymentTransaction { get; set; }
    public ICollection<InvoiceRefundItem> Items { get; set; } = new List<InvoiceRefundItem>();
}
public sealed class InvoiceRefundItem
{
    public int Id { get; set; }
    public int InvoiceRefundId { get; set; }
    public int InvoiceItemId { get; set; }
    public int Quantity { get; set; }
    public InvoiceRefund InvoiceRefund { get; set; } = null!;
    public InvoiceItem InvoiceItem { get; set; } = null!;
}
