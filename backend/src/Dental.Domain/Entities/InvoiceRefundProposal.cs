namespace Dental.Domain.Entities;

/// <summary>MOD_BIL: immutable refund scope submitted by the receptionist; only decision fields may change.</summary>
public sealed class InvoiceRefundProposal
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string Snapshot { get; set; } = "";
    public Guid RequestId { get; set; }
    public int CreatorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ReasonCode { get; set; } = "";
    public string ReasonNotes { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public int? ReviewerId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNotes { get; set; }
    public int? RefundId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public User? Reviewer { get; set; }
    public ICollection<InvoiceRefundProposalItem> Items { get; set; } = new List<InvoiceRefundProposalItem>();
}

public sealed class InvoiceRefundProposalItem
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int InvoiceItemId { get; set; }
    public int Quantity { get; set; }
    public InvoiceRefundProposal Proposal { get; set; } = null!;
    public InvoiceItem InvoiceItem { get; set; } = null!;
}