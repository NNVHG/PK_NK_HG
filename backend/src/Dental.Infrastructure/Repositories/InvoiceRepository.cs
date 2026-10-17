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
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class InvoiceRepository(DentalDbContext db) : IInvoiceRepository
{
    public Task<Invoice?> GetForVisitAsync(int visitId, CancellationToken ct = default)
        => db.Invoices.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.VisitId == visitId && x.Status != InvoiceStatus.Cancelled, ct);
    public async Task<Result> StageVisitCancellationAsync(int visitId, int userId, string role, string? ip, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Visit invoice cancellation requires the caller's Serializable transaction.");
        var stored = await db.Visits.AsNoTracking().SingleOrDefaultAsync(v => v.VisitId == visitId, ct);
        if (stored is null) return Result.Failure(Error.NotFound);
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist) || role == RoleCodes.Dentist && stored.DentistId != userId ||
            !await db.Users.AnyAsync(u => u.UserId == userId && u.IsActive && u.Role.RoleCode == role, ct))
            return Result.Failure(Error.Forbidden);
        if (stored.Status != VisitStatuses.InProgress || stored.IsLocked)
            return Result.Failure(new Error("BIL_012", "Ca khám đã thay đổi; tải lại trước khi hủy."));
        // Any historical receipt blocks this derived operation, even if PaidAmount was reset elsewhere.
        if (await db.Invoices.AnyAsync(i => i.VisitId == visitId && i.PaidAmount != 0, ct) ||
            await db.PaymentTransactions.AnyAsync(p => p.Invoice.VisitId == visitId, ct))
            return Result.Failure(new Error("BIL_013", "Ca khám đã phát sinh thanh toán; không thể tự hủy hóa đơn."));
        var active = await db.Invoices.Where(i => i.VisitId == visitId && i.Status != InvoiceStatus.Cancelled).ToListAsync(ct);
        if (active.Any(i => i.Status != InvoiceStatus.Draft))
            return Result.Failure(new Error("BIL_013", "Chỉ tự hủy hóa đơn Draft chưa thu cùng ca khám; tải lại để đối chiếu."));
        var prescription = await db.Prescriptions.SingleOrDefaultAsync(p => p.VisitId == visitId && !p.IsCancelled, ct);
        if (prescription?.DispensedAt is not null)
            return Result.Failure(new Error("RX_002", "Đơn thuốc đã cấp phát; không thể hủy ca thông thường."));
        if (prescription is not null && !prescription.IsCancelled)
        {
            prescription.IsCancelled = true; prescription.Version++; prescription.UpdatedAt = DateTime.UtcNow;
            db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "MOD_RX_PRESCRIPTION_CANCELLED_WITH_VISIT",
                EntityType = "Prescription", EntityId = prescription.Id, IpAddress = ip, CreatedAt = DateTime.UtcNow,
                Detail = JsonSerializer.Serialize(new { visitId, prescription.Version, trigger = "visit_cancelled" }) });
        }
        foreach (var invoice in active)
        {
            invoice.Status = InvoiceStatus.Cancelled;
            db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "MOD_BIL_DRAFT_CANCELLED_WITH_VISIT",
                EntityType = "Invoice", EntityId = invoice.Id, IpAddress = ip, CreatedAt = DateTime.UtcNow,
                Detail = JsonSerializer.Serialize(new { visitId, trigger = "visit_cancelled" }) });
        }
        // Keep code, amounts and item snapshots. Queue history in this transaction retains the cancellation reason.
        return Result.Success();
    }

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
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
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
            var prescription = await db.Prescriptions.Include(p => p.Items).SingleOrDefaultAsync(p => p.VisitId == visitId && !p.IsCancelled, ct);
            var synced = InvoiceDraftSynchronizer.Apply(invoice, rows, prescription?.Items);
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
            if (prescription is not null) prescription.Invoice = invoice;
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
            if (transaction is not null) await transaction.CommitAsync(ct);
            committed = true;
            return Result<InvoiceDraftSynchronization>.Success(new(invoice, created || synced.Value));
        }
        catch (Exception error) when (IsConflict(error))
        {
            if (!ownsTransaction) throw;
            try { if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None); }
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
