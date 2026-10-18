using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;

namespace Dental.Application.Interfaces;

public interface IInvoiceRefundProposalRepository
{
    Task<Result<InvoiceRefundProposalDto>> CreateAsync(int invoiceId, int actorId, InvoiceRefundProposalCreateRequest request, CancellationToken ct);
    Task<Result<IReadOnlyList<InvoiceRefundProposalDto>>> ListPendingAsync(int actorId, int? invoiceId, CancellationToken ct);
    Task<Result<InvoiceRefundProposalDto>> ReadAsync(int proposalId, int actorId, CancellationToken ct);
    Task<Result<bool>> RejectAsync(int proposalId, int actorId, string? decisionNotes, string? ip, CancellationToken ct);
    Task<Result<bool>> CompleteApprovalAsync(int proposalId, int actorId, int refundId, string? ip, CancellationToken ct);
}