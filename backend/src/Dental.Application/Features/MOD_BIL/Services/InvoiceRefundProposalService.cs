using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;

namespace Dental.Application.Features.MOD_BIL.Services;

public sealed class InvoiceRefundProposalService(IInvoiceRefundProposalRepository proposals, InvoiceRefundService refunds)
{
    public Task<Result<InvoiceRefundProposalDto>> CreateAsync(int invoiceId, int actorId, string? role, InvoiceRefundProposalCreateRequest request, CancellationToken ct)
    {
        if (role != RoleCodes.Receptionist) return Task.FromResult(Result<InvoiceRefundProposalDto>.Failure(Error.Forbidden));
        if (invoiceId <= 0 || actorId <= 0 || request is null || !new InvoiceRefundProposalCreateValidator().Validate(request).IsValid)
            return Task.FromResult(Result<InvoiceRefundProposalDto>.Failure(Error.Validation));
        return proposals.CreateAsync(invoiceId, actorId, request, ct);
    }

    public Task<Result<IReadOnlyList<InvoiceRefundProposalDto>>> ListPendingAsync(int actorId, string? role, int? invoiceId, CancellationToken ct)
    {
        if (role != RoleCodes.Admin) return Task.FromResult(Result<IReadOnlyList<InvoiceRefundProposalDto>>.Failure(Error.Forbidden));
        if (actorId <= 0 || invoiceId is <= 0) return Task.FromResult(Result<IReadOnlyList<InvoiceRefundProposalDto>>.Failure(Error.Validation));
        return proposals.ListPendingAsync(actorId, invoiceId, ct);
    }

    public Task<Result<InvoiceRefundProposalDto>> ReadAsync(int id, int actorId, string? role, CancellationToken ct)
    {
        if (role != RoleCodes.Admin) return Task.FromResult(Result<InvoiceRefundProposalDto>.Failure(Error.Forbidden));
        return id <= 0 || actorId <= 0 ? Task.FromResult(Result<InvoiceRefundProposalDto>.Failure(Error.Validation)) : proposals.ReadAsync(id, actorId, ct);
    }

    public async Task<Result<InvoiceRefundPreview>> PreviewAsync(int id, int actorId, string? role, InvoiceRefundProposalPreviewRequest request, CancellationToken ct)
    {
        if (role != RoleCodes.Admin) return Result<InvoiceRefundPreview>.Failure(Error.Forbidden);
        if (id <= 0 || actorId <= 0 || request is null || !new InvoiceRefundProposalPreviewValidator().Validate(request).IsValid) return Result<InvoiceRefundPreview>.Failure(Error.Validation);
        var result = await proposals.ReadAsync(id, actorId, ct);
        if (result.IsFailure) return Result<InvoiceRefundPreview>.Failure(result.Error);
        var proposal = result.Value!;
        if (proposal.Status != "Pending") return Result<InvoiceRefundPreview>.Failure(Error.Conflict);
        return await refunds.PreviewAsync(proposal.InvoiceId, actorId, role,
            new(proposal.Snapshot, proposal.Items.Select(x => new RefundSelection(x.InvoiceItemId, x.Quantity)).ToArray(), request.PaymentMethod), ct);
    }

    public async Task<Result<RefundRecordDto>> ApproveAsync(int id, int actorId, string? role, InvoiceRefundProposalDecisionRequest request, string? ip, CancellationToken ct)
    {
        if (role != RoleCodes.Admin) return Result<RefundRecordDto>.Failure(Error.Forbidden);
        if (id <= 0 || actorId <= 0 || request is null || !new InvoiceRefundProposalDecisionValidator().Validate(request).IsValid) return Result<RefundRecordDto>.Failure(Error.Validation);
        var result = await proposals.ReadAsync(id, actorId, ct);
        if (result.IsFailure) return Result<RefundRecordDto>.Failure(result.Error);
        var proposal = result.Value!;
        if (proposal.Status is "Approved" or "Stale")
        {
            var context = await refunds.ReadAsync(proposal.InvoiceId, actorId, role, ct);
            if (context.IsFailure) return Result<RefundRecordDto>.Failure(context.Error);
            var prior = context.Value!.History.FirstOrDefault(x => x.RequestId == proposal.RequestId);
            if (prior is not null)
            {
                if (proposal.Status != "Approved")
                {
                    var recovered = await proposals.CompleteApprovalAsync(id, actorId, prior.Id, ip, ct);
                    if (recovered.IsFailure) return Result<RefundRecordDto>.Failure(recovered.Error);
                }
                return Result<RefundRecordDto>.Success(prior);
            }
        }
        if (proposal.Status != "Pending") return Result<RefundRecordDto>.Failure(Error.Conflict);
        var refundRequest = new InvoiceRefundRequest(proposal.Snapshot,
            proposal.Items.Select(x => new RefundSelection(x.InvoiceItemId, x.Quantity)).ToArray(), request.PaymentMethod,
            request.ExpectedPreview, proposal.RequestId, proposal.ReasonCode, proposal.ReasonNotes, request.Confirmed,
            request.LateApproval, request.ReserveConfirmed);
        var applied = await refunds.RefundAsync(proposal.InvoiceId, actorId, role, refundRequest, ip, ct);
        if (applied.IsFailure) return applied;
        var completed = await proposals.CompleteApprovalAsync(id, actorId, applied.Value!.Id, ip, ct);
        return completed.IsFailure ? Result<RefundRecordDto>.Failure(completed.Error) : applied;
    }

    public Task<Result<bool>> RejectAsync(int id, int actorId, string? role, InvoiceRefundProposalRejectRequest request, string? ip, CancellationToken ct)
    {
        if (role != RoleCodes.Admin) return Task.FromResult(Result<bool>.Failure(Error.Forbidden));
        if (id <= 0 || actorId <= 0 || request is null || !new InvoiceRefundProposalRejectValidator().Validate(request).IsValid)
            return Task.FromResult(Result<bool>.Failure(Error.Validation));
        return proposals.RejectAsync(id, actorId, request.DecisionNotes?.Trim(), ip, ct);
    }
}