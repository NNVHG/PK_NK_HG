using System.Data;
using System.Globalization;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class InvoiceRefundProposalRepository(DentalDbContext db, VietnamClock clock) : IInvoiceRefundProposalRepository
{
    private static readonly Error Changed = new("BIL_REFUND_PROPOSAL_CONFLICT", "Đề xuất đã thay đổi hoặc hóa đơn không còn khớp ảnh chụp lúc gửi; hãy tạo đề xuất mới.");
    private static readonly Error Conflict = new("BIL_REFUND_PROPOSAL_STATE", "Đề xuất không còn ở trạng thái chờ quyết định.");
    private static string Money(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);

    private Task<bool> IsReceptionist(int actor, CancellationToken ct) => db.Users.AnyAsync(x => x.UserId == actor && x.IsActive && x.Role.RoleCode == RoleCodes.Receptionist, ct);
    private Task<bool> IsAdmin(int actor, CancellationToken ct) => db.Users.AnyAsync(x => x.UserId == actor && x.IsActive && x.Role.RoleCode == RoleCodes.Admin, ct);

    private IQueryable<InvoiceRefund> ProposalQuery() => db.InvoiceRefunds
        .Include(x => x.Invoice).ThenInclude(x => x.Items)
        .Include(x => x.Invoice).ThenInclude(x => x.Visit)
        .Include(x => x.Invoice).ThenInclude(x => x.Refunds)
        .Include(x => x.Actor).Include(x => x.Reviewer)
        .AsSplitQuery();

    private static InvoiceRefundProposalDto Map(InvoiceRefund proposal)
    {
        var invoiceItems = proposal.Invoice.Items.ToDictionary(x => x.Id);
        return new InvoiceRefundProposalDto(
            proposal.Id,
            proposal.InvoiceId,
            proposal.Invoice.InvoiceCode,
            proposal.Snapshot,
            proposal.RequestId,
            proposal.Status,
            proposal.CreatedAt,
            proposal.Actor.FullName,
            proposal.ReasonCode,
            proposal.ReasonNotes,
            proposal.GetItems()
                .OrderBy(x => x.InvoiceItemId)
                .Select(x =>
                {
                    var item = invoiceItems[x.InvoiceItemId];
                    return new RefundProposalLineDto(x.InvoiceItemId, item.ItemType, item.Code, item.Name, x.Quantity, Money(item.UnitPrice));
                })
                .ToArray(),
            proposal.Status == InvoiceRefundStatuses.Approved ? proposal.Id : null,
            proposal.DecisionNotes);
    }

    private async Task<Invoice?> InvoiceForRefund(int id, CancellationToken ct)
    {
        var invoice = await db.Invoices.Include(x => x.Visit).Include(x => x.Items).Include(x => x.Refunds)
            .AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (invoice is not null)
            foreach (var r in invoice.Refunds) r.HydrateItems();
        return invoice;
    }

    public async Task<Result<InvoiceRefundProposalDto>> CreateAsync(int invoiceId, int actorId, InvoiceRefundProposalCreateRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (!await IsReceptionist(actorId, ct)) return Result<InvoiceRefundProposalDto>.Failure(Error.Forbidden);
            var invoice = await InvoiceForRefund(invoiceId, ct);
            if (invoice is null) return Result<InvoiceRefundProposalDto>.Failure(Error.NotFound);
            var calculation = InvoiceRefundRules.Calculate(invoice, request.Items);
            if (calculation.IsFailure) return Result<InvoiceRefundProposalDto>.Failure(calculation.Error);

            var proposal = new InvoiceRefund
            {
                InvoiceId = invoiceId,
                Invoice = invoice,
                Revision = 0,
                Snapshot = InvoiceRefundSnapshot.Create(invoice),
                PayloadHash = string.Empty,
                RequestId = Guid.NewGuid(),
                ActorId = actorId,
                Actor = await db.Users.SingleAsync(x => x.UserId == actorId, ct),
                CreatedAt = clock.UtcNow,
                ReasonCode = request.ReasonCode,
                ReasonNotes = request.ReasonNotes.Trim(),
                Status = InvoiceRefundStatuses.Pending,
                PaymentMethod = "Cash",
                Items = request.Items.Select(selection => new InvoiceRefundItem
                {
                    InvoiceItemId = selection.InvoiceItemId,
                    Quantity = selection.Quantity,
                }).ToList()
            };
            proposal.SyncItemsJson();

            db.InvoiceRefunds.Add(proposal);
            await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog
            {
                UserId = actorId, EntityType = "InvoiceRefundProposal", EntityId = proposal.Id,
                Action = "MOD_BIL_REFUND_PROPOSAL_CREATED", CreatedAt = clock.UtcNow,
                Detail = JsonSerializer.Serialize(new { invoiceId, proposalId = proposal.Id, proposal.ReasonCode, itemCount = proposal.Items.Count })
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            committed = true;
            return Result<InvoiceRefundProposalDto>.Success(Map(proposal));
        }
        catch (Exception error) when (IsConflict(error)) { return Result<InvoiceRefundProposalDto>.Failure(Changed); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }

    public async Task<Result<IReadOnlyList<InvoiceRefundProposalDto>>> ListPendingAsync(int actorId, int? invoiceId, CancellationToken ct)
    {
        if (!await IsAdmin(actorId, ct)) return Result<IReadOnlyList<InvoiceRefundProposalDto>>.Failure(Error.Forbidden);
        var ids = await db.InvoiceRefunds.AsNoTracking()
            .Where(x => (x.Status == InvoiceRefundStatuses.Pending || x.Status == InvoiceRefundStatuses.Stale) && (invoiceId == null || x.InvoiceId == invoiceId))
            .OrderByDescending(x => x.CreatedAt).Take(100).Select(x => x.Id).ToArrayAsync(ct);
        var results = new List<InvoiceRefundProposalDto>(ids.Length);
        foreach (var id in ids)
        {
            var item = await ReadAsync(id, actorId, ct);
            if (item.IsSuccess) results.Add(item.Value!);
        }
        return Result<IReadOnlyList<InvoiceRefundProposalDto>>.Success(results);
    }

    public async Task<Result<InvoiceRefundProposalDto>> ReadAsync(int proposalId, int actorId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await IsAdmin(actorId, ct)) return Result<InvoiceRefundProposalDto>.Failure(Error.Forbidden);
        var proposal = await ProposalQuery().SingleOrDefaultAsync(x => x.Id == proposalId, ct);
        if (proposal is null) return Result<InvoiceRefundProposalDto>.Failure(Error.NotFound);
        foreach (var r in proposal.Invoice.Refunds) r.HydrateItems();
        if (proposal.Status == InvoiceRefundStatuses.Pending && InvoiceRefundSnapshot.Create(proposal.Invoice) != proposal.Snapshot)
        {
            proposal.Status = InvoiceRefundStatuses.Stale;
            proposal.ReviewerId = actorId;
            proposal.DecidedAt = clock.UtcNow;
            proposal.DecisionNotes = "Hóa đơn đã thay đổi; cần gửi đề xuất mới.";
            db.AuditLogs.Add(new AuditLog
            {
                UserId = actorId, EntityType = "InvoiceRefundProposal", EntityId = proposal.Id,
                Action = "MOD_BIL_REFUND_PROPOSAL_STALE", CreatedAt = clock.UtcNow,
                Detail = JsonSerializer.Serialize(new { proposalId = proposal.Id, invoiceId = proposal.InvoiceId })
            });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Result<InvoiceRefundProposalDto>.Success(Map(proposal));
    }

    public async Task<Result<bool>> RejectAsync(int proposalId, int actorId, string? decisionNotes, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await IsAdmin(actorId, ct)) return Result<bool>.Failure(Error.Forbidden);
        var proposal = await ProposalQuery().SingleOrDefaultAsync(x => x.Id == proposalId, ct);
        if (proposal is null) return Result<bool>.Failure(Error.NotFound);
        foreach (var r in proposal.Invoice.Refunds) r.HydrateItems();
        if (proposal.Status != InvoiceRefundStatuses.Pending) return Result<bool>.Failure(Conflict);
        if (InvoiceRefundSnapshot.Create(proposal.Invoice) != proposal.Snapshot)
        {
            proposal.Status = InvoiceRefundStatuses.Stale; proposal.ReviewerId = actorId; proposal.DecidedAt = clock.UtcNow;
            proposal.DecisionNotes = "Hóa đơn đã thay đổi; cần gửi đề xuất mới.";
            db.AuditLogs.Add(Audit(proposal, actorId, "MOD_BIL_REFUND_PROPOSAL_STALE", ip));
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Result<bool>.Failure(Changed);
        }
        proposal.Status = InvoiceRefundStatuses.Rejected;
        proposal.ReviewerId = actorId;
        proposal.DecidedAt = clock.UtcNow;
        proposal.DecisionNotes = string.IsNullOrWhiteSpace(decisionNotes) ? null : decisionNotes.Trim();
        db.AuditLogs.Add(Audit(proposal, actorId, "MOD_BIL_REFUND_PROPOSAL_REJECTED", ip));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> CompleteApprovalAsync(int proposalId, int actorId, int refundId, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (!await IsAdmin(actorId, ct)) return Result<bool>.Failure(Error.Forbidden);
            var proposal = await db.InvoiceRefunds.SingleOrDefaultAsync(x => x.Id == proposalId, ct);
            if (proposal is null) return Result<bool>.Failure(Error.NotFound);
            var matches = proposal.Id == refundId && proposal.Status == InvoiceRefundStatuses.Approved;
            if (!matches) return Result<bool>.Failure(Error.Conflict);

            if (!await db.AuditLogs.AnyAsync(a => a.EntityType == "InvoiceRefundProposal" && a.EntityId == proposalId && a.Action == "MOD_BIL_REFUND_PROPOSAL_APPROVED", ct))
            {
                db.AuditLogs.Add(Audit(proposal, actorId, "MOD_BIL_REFUND_PROPOSAL_APPROVED", ip));
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct); committed = true;
            return Result<bool>.Success(true);
        }
        catch (Exception error) when (IsConflict(error)) { return Result<bool>.Failure(Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }

    private AuditLog Audit(InvoiceRefund proposal, int actorId, string action, string? ip) => new()
    {
        UserId = actorId,
        EntityType = "InvoiceRefundProposal",
        EntityId = proposal.Id,
        Action = action,
        CreatedAt = clock.UtcNow,
        IpAddress = ip,
        Detail = JsonSerializer.Serialize(new
        {
            proposalId = proposal.Id,
            invoiceId = proposal.InvoiceId,
            status = proposal.Status,
            refundId = proposal.Status == InvoiceRefundStatuses.Approved ? (int?)proposal.Id : null
        })
    };

    private static bool IsConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException || current is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
