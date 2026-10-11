using System.Data;
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

    public async Task<Result<Invoice>> SaveCompletedVisitDraftAsync(Visit visit, int userId, DateOnly date, CancellationToken ct = default)
    {
        var result = await SaveAsync(visit.VisitId, userId, date, visit, ct);
        return result.IsFailure ? Result<Invoice>.Failure(result.Error) : Result<Invoice>.Success(result.Value!.Invoice);
    }

    private async Task<Result<InvoiceDraftSynchronization>> SaveAsync(int visitId, int userId, DateOnly date, Visit? completingVisit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            // AsNoTracking reads persisted InProgress state; the completion path has pending tracked mutations.
            var stored = await db.Visits.AsNoTracking().FirstOrDefaultAsync(x => x.VisitId == visitId, ct);
            if (stored is null) return Fail(Error.NotFound);
            if (stored.Status != VisitStatuses.InProgress || stored.IsLocked ||
                completingVisit is not null && stored.DentistId != completingVisit.DentistId)
                return Fail(new Error("BIL_003", "Trạng thái lần khám đã thay đổi; tải lại hồ sơ."));
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
            if (completingVisit is not null) invoice.Status = InvoiceStatus.PendingPayment;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            committed = true;
            return Result<InvoiceDraftSynchronization>.Success(new(invoice, created || synced.Value));
        }
        catch (Exception error) when (error is PostgresException { SqlState: "40001" or "40P01" or "23505" }
            || error is DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" or "23505" } })
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Fail(new Error("BIL_006", "Có thao tác đồng thời; tải lại hồ sơ trước khi thử lại."));
        }
        finally
        {
            if (!committed) db.ChangeTracker.Clear();
        }
    }

    private static Result<InvoiceDraftSynchronization> Fail(Error error) => Result<InvoiceDraftSynchronization>.Failure(error);
}
