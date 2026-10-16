using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class InvoiceRepository(DentalDbContext db) : IInvoiceRepository
{
    public Task<Invoice?> GetForVisitAsync(int visitId, CancellationToken ct = default)
        => db.Invoices.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.VisitId == visitId && x.Status != InvoiceStatus.Cancelled, ct);
    public Task<int?> GetQueueIdAsync(int visitId, CancellationToken ct = default)
        => db.QueueEntries.Where(x => x.VisitId == visitId).Select(x => (int?)x.QueueEntryId).FirstOrDefaultAsync(ct);

    public Task<Result<InvoiceDraftSynchronization>> SynchronizeDraftAsync(int visitId, int userId, DateOnly date, CancellationToken ct = default)
        => SaveAsync(visitId, userId, date, null, ct);

    public Task<Result<InvoiceDraftSynchronization>> CompleteCorrectionAsync(int visitId, int userId, string role,
        DateOnly date, string? ip, CancellationToken ct = default)
        => SaveAsync(visitId, userId, date, null, ct, role, ip);

    public async Task<Result<Invoice>> SaveCompletedVisitDraftAsync(Visit visit, int userId, DateOnly date, CancellationToken ct = default)
    {
        var result = await SaveAsync(visit.VisitId, userId, date, visit, ct);
        return result.IsFailure ? Result<Invoice>.Failure(result.Error) : Result<Invoice>.Success(result.Value!.Invoice);
    }

    private async Task<Result<InvoiceDraftSynchronization>> SaveAsync(int visitId, int userId, DateOnly date, Visit? completingVisit,
        CancellationToken ct, string? correctionRole = null, string? ip = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            // Read persisted state; ordinary completion has pending tracked Visit/Queue mutations.
            var stored = await db.Visits.AsNoTracking().FirstOrDefaultAsync(x => x.VisitId == visitId, ct);
            if (stored is null) return Fail(Error.NotFound);
            if (!VisitStatuses.IsEditable(stored.Status) || stored.IsLocked ||
                completingVisit is not null && stored.DentistId != completingVisit.DentistId)
                return Fail(new Error("BIL_003", "Trạng thái lần khám đã thay đổi; tải lại hồ sơ."));
            var finalizingCorrection = correctionRole is not null;
            if (completingVisit is not null && stored.Status != VisitStatuses.InProgress ||
                finalizingCorrection && stored.Status != VisitStatuses.Correcting)
                return Fail(new Error("BIL_003", "Trạng thái lần khám đã thay đổi; tải lại hồ sơ."));
            if (finalizingCorrection)
            {
                if (correctionRole is not (RoleCodes.Admin or RoleCodes.Dentist) ||
                    correctionRole == RoleCodes.Dentist && stored.DentistId != userId)
                    return Fail(Error.Forbidden);
                if (!await db.Users.AnyAsync(u => u.UserId == userId && u.IsActive && u.Role.RoleCode == correctionRole, ct))
                    return Fail(Error.Forbidden);
                if (!await db.QueueEntries.AnyAsync(q => q.VisitId == visitId && q.Status == QueueStatus.Completed, ct))
                    return Fail(new Error("BIL_011", "Lượt khám đã thay đổi; không thể hoàn tất đính chính."));
            }
            // Correction must never resurrect money already collected on a previous invoice.
            if (stored.Status == VisitStatuses.Correcting &&
                (await db.Invoices.AnyAsync(i => i.VisitId == visitId && i.PaidAmount != 0, ct) ||
                 await db.PaymentTransactions.AnyAsync(p => p.Invoice.VisitId == visitId && p.Amount > 0, ct)))
                return Fail(new Error("BIL_010", "Hồ sơ đã phát sinh thanh toán; không thể đính chính."));
            var invoice = await db.Invoices.Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.VisitId == visitId && x.Status != InvoiceStatus.Cancelled, ct);
            // Never recalculate or promote a paid/pending invoice from an unrelated clinical state.
            if (invoice is not null && (invoice.Status != InvoiceStatus.Draft || invoice.PaidAmount != 0))
                return Fail(new Error("BIL_007", "Chỉ đồng bộ hóa đơn nháp chưa thanh toán."));
            var created = invoice is null;
            invoice ??= new Invoice { VisitId = visitId, CreatedByUserId = userId, CreatedAt = DateTime.UtcNow, Status = InvoiceStatus.Draft };
            var rows = await db.VisitServices.AsNoTracking().Where(x => x.VisitId == visitId).ToListAsync(ct);
            var oldItems = invoice.Items.ToList();
            var synced = InvoiceDraftSynchronizer.Apply(invoice, rows);
            if (synced.IsFailure) return Fail(synced.Error);
            if (!created && synced.Value) db.InvoiceItems.RemoveRange(oldItems);
            if (created)
            {
                var counter = await db.InvoiceNumberCounters.FindAsync([date], ct);
                if (counter is null) { counter = new InvoiceNumberCounter { InvoiceDate = date }; db.InvoiceNumberCounters.Add(counter); }
                if (counter.LastNumber >= 9999) return Fail(new Error("BIL_005", "Đã hết số hóa đơn trong ngày."));
                counter.LastNumber++;
                invoice.InvoiceCode = $"INV-{date:yyyyMMdd}-{counter.LastNumber:D4}";
                db.Invoices.Add(invoice);
            }
            // Keep the same identity/code when the initial Draft is finalized (DL-134, DL-144).
            if (completingVisit is not null || finalizingCorrection) invoice.Status = InvoiceStatus.PendingPayment;
            if (finalizingCorrection)
            {
                var visit = await db.Visits.SingleAsync(v => v.VisitId == visitId, ct);
                visit.Status = VisitStatuses.Completed; visit.IsLocked = true;
                visit.LockedAt = DateTime.UtcNow; visit.LockedBy = userId; visit.UpdatedAt = DateTime.UtcNow;
                // Keep StartedAt/EndedAt and the completed queue; correction is outside physical scheduling.
            }
            await db.SaveChangesAsync(ct);
            if (finalizingCorrection)
            {
                db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "MOD_PAT_VISIT_CORRECTION_COMPLETED",
                    EntityType = "Visit", EntityId = visitId, IpAddress = ip, CreatedAt = DateTime.UtcNow,
                    Detail = JsonSerializer.Serialize(new { invoiceId = invoice.Id, mode = VisitStatuses.Correcting }) });
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            committed = true;
            return Result<InvoiceDraftSynchronization>.Success(new(invoice, created || synced.Value));
        }
        catch (Exception error) when (IsConflict(error))
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (InvalidOperationException) { /* PostgreSQL can already have aborted a failed Serializable commit. */ }
            return Fail(new Error("BIL_006", "Có thao tác đồng thời; tải lại hồ sơ trước khi thử lại."));
        }
        finally
        {
            if (!committed) db.ChangeTracker.Clear();
        }
    }

    private static Result<InvoiceDraftSynchronization> Fail(Error error) => Result<InvoiceDraftSynchronization>.Failure(error);

    private static bool IsConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException or PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
