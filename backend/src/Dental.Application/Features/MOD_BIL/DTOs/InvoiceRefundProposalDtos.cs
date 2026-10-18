namespace Dental.Application.Features.MOD_BIL.DTOs;

public sealed record InvoiceRefundProposalCreateRequest(IReadOnlyList<RefundSelection> Items, string ReasonCode, string ReasonNotes);
public sealed record InvoiceRefundProposalPreviewRequest(string PaymentMethod);
public sealed record InvoiceRefundProposalDecisionRequest(string PaymentMethod, string ExpectedPreview, bool Confirmed, bool LateApproval, bool ReserveConfirmed);
public sealed record InvoiceRefundProposalRejectRequest(string? DecisionNotes = null);
public sealed record RefundProposalLineDto(int InvoiceItemId, string ItemType, string Code, string Name, int Quantity, string UnitPrice);
public sealed record InvoiceRefundProposalDto(int Id, int InvoiceId, string InvoiceCode, string Snapshot, Guid RequestId,
    string Status, DateTime CreatedAt, string RequesterName, string ReasonCode, string ReasonNotes,
    IReadOnlyList<RefundProposalLineDto> Items, int? RefundId, string? DecisionNotes);