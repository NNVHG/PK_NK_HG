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

public sealed class VisitReopenRepository(DentalDbContext db) : IVisitReopenRepository
{
    public async Task<IReadOnlyList<VisitUnlockRecord>> GetHistoryAsync(int visitId, CancellationToken ct = default)
        => await db.VisitUnlockRecords.AsNoTracking().Where(x => x.VisitId == visitId).OrderByDescending(x => x.Id).ToListAsync(ct);
    public async Task<Result<VisitUnlockRecord>> ReopenAsync(int visitId, int actor, string reason, string? ip, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            var visit = await db.Visits.FirstOrDefaultAsync(x => x.VisitId == visitId, ct);
            if (visit is null) return Fail(Error.NotFound);
            var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.VisitId == visitId && x.Status != InvoiceStatus.Cancelled, ct);
            var otherOpen = await db.Visits.AnyAsync(x => x.PatientId == visit.PatientId && x.VisitId != visitId &&
                (x.Status == VisitStatuses.Created || x.Status == VisitStatuses.InProgress), ct);
            var hasPreviousPayment = await db.Invoices.AnyAsync(x => x.VisitId == visitId && x.PaidAmount != 0, ct);
            var validation = VisitReopenRules.Validate(visit, invoice, otherOpen, hasPreviousPayment);
            if (validation.IsFailure) return Fail(validation.Error);
            var queue = await db.QueueEntries.FirstOrDefaultAsync(x => x.VisitId == visitId, ct);
            if (queue is null || queue.Status != QueueStatus.Completed)
                return Fail(new Error("BIL_011", "Không thể mở lại lượt khám có trạng thái hàng đợi không phù hợp."));
            var now = DateTime.UtcNow;
            if (invoice is not null) invoice.Status = InvoiceStatus.Cancelled;
            visit.IsLocked = false; visit.Status = VisitStatuses.InProgress; visit.EndedAt = null; visit.UpdatedAt = now;
            queue.Status = QueueStatus.InConsultation; queue.UpdatedAt = now;
            db.QueueStatusHistories.Add(new QueueStatusHistory { QueueEntryId = queue.QueueEntryId,
                FromStatus = QueueStatus.Completed, ToStatus = QueueStatus.InConsultation, ChangedByUserId = actor,
                ChangedAt = now, Reason = "Mở lại khám theo xác nhận Admin" });
            var record = new VisitUnlockRecord { VisitId = visitId, ActorUserId = actor, Reason = reason,
                CancelledInvoiceId = invoice?.Id, CreatedAt = now };
            db.VisitUnlockRecords.Add(record);
            await db.SaveChangesAsync(ct);
            // [CẦN XÁC NHẬN] DL-158 yêu cầu lý do nguyên văn trong audit; AGENTS §6 cấm thông tin bệnh nhân.
            // Lưu lý do ở bản ghi được bảo vệ, audit chỉ tham chiếu để tránh rò nội dung bệnh án.
            var detail = JsonSerializer.Serialize(new { unlockRecordId = record.Id, cancelledInvoiceId = record.CancelledInvoiceId });
            db.AuditLogs.Add(new AuditLog { UserId = actor, Action = "MOD_PAT_VISIT_UNLOCKED", EntityType = "Visit",
                EntityId = visitId, Detail = detail, IpAddress = ip, CreatedAt = now });
            if (invoice is not null)
                db.AuditLogs.Add(new AuditLog { UserId = actor, Action = "MOD_BIL_DRAFT_CANCELLED_FOR_CORRECTION",
                    EntityType = "Invoice", EntityId = invoice.Id, Detail = detail, IpAddress = ip, CreatedAt = now });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct); committed = true;
            return Result<VisitUnlockRecord>.Success(record);
        }
        catch (Exception error) when (error is PostgresException { SqlState: "40001" or "40P01" or "23505" } ||
            error is DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" or "23505" } })
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Fail(new Error("BIL_006", "Có thao tác đồng thời; tải lại hồ sơ trước khi thử lại."));
        }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }

    private static Result<VisitUnlockRecord> Fail(Error error) => Result<VisitUnlockRecord>.Failure(error);
}
