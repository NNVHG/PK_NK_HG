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

    public async Task<Result<Invoice>> SaveCompletedVisitDraftAsync(Visit visit, int userId, DateOnly date, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            var existing = await GetForVisitAsync(visit.VisitId, ct);
            var stored = await db.Visits.AsNoTracking().FirstOrDefaultAsync(x => x.VisitId == visit.VisitId, ct);
            if (stored is null) return Result<Invoice>.Failure(Error.NotFound);
            if (existing is not null)
                return Result<Invoice>.Failure(new Error("BIL_002", "Lần khám đã có hóa đơn; tải lại hồ sơ."));
            if (stored.Status != VisitStatuses.InProgress || stored.IsLocked || stored.DentistId != visit.DentistId)
                return Result<Invoice>.Failure(new Error("BIL_003", "Trạng thái lần khám đã thay đổi; tải lại hồ sơ."));
            var rows = await db.VisitServices.AsNoTracking().Where(x => x.VisitId == visit.VisitId).ToListAsync(ct);
            var snapshot = InvoiceServiceSnapshot.Create(rows);
            if (snapshot.IsFailure) return Result<Invoice>.Failure(snapshot.Error);
            var invoice = new Invoice { VisitId = visit.VisitId, CreatedByUserId = userId, CreatedAt = DateTime.UtcNow,
                Status = InvoiceStatus.PendingPayment, Items = snapshot.Value!.ToList(),
                TotalAmount = snapshot.Value!.Sum(x => x.TotalAmount) };
            var counter = await db.InvoiceNumberCounters.FindAsync([date], ct);
            if (counter is null) { counter = new InvoiceNumberCounter { InvoiceDate = date }; db.InvoiceNumberCounters.Add(counter); }
            if (counter.LastNumber >= 9999)
                return Result<Invoice>.Failure(new Error("BIL_005", "Đã hết số hóa đơn trong ngày."));
            counter.LastNumber++;
            invoice.InvoiceCode = $"INV-{date:yyyyMMdd}-{counter.LastNumber:D4}";
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            committed = true;
            return Result<Invoice>.Success(invoice);
        }
        catch (Exception error) when (error is PostgresException { SqlState: "40001" or "40P01" or "23505" }
            || error is DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" or "23505" } })
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<Invoice>.Failure(new Error("BIL_006", "Có thao tác đồng thời; tải lại hồ sơ trước khi thử lại."));
        }
        finally
        {
            // Các mutation Visit/Queue/history đang chờ cũng phải bị loại nếu hóa đơn không được lưu.
            if (!committed) db.ChangeTracker.Clear();
        }
    }
}
