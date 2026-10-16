using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Dental.Infrastructure.Repositories;
public sealed class FdiServiceDeletionRepository(DentalDbContext db) : IFdiServiceDeletionRepository
{
    public async Task<Result> DeleteAsync(DeleteVisitServiceRequest request, int actorId, string role, string? ip, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Dentist)) return Result.Failure(Error.Forbidden);
            var visit = await db.Visits.SingleOrDefaultAsync(v => v.VisitId == request.VisitId, ct);
            if (visit is null) return Result.Failure(Error.NotFound);
            if (role == RoleCodes.Dentist && visit.DentistId != actorId) return Result.Failure(Error.Forbidden);
            if (!VisitStatuses.IsEditable(visit.Status) || visit.IsLocked)
                return Result.Failure("FDI_043", "Chỉ được xóa dịch vụ khi lần khám đang diễn ra và chưa khóa.");
            var row = await db.VisitServices.SingleOrDefaultAsync(s => s.VisitId == request.VisitId && s.Id == request.AssignmentId, ct);
            if (row is null) return Result.Failure(Error.NotFound);
            var invoices = await db.Invoices.Include(i => i.Items).Where(i => i.VisitId == request.VisitId).ToListAsync(ct);
            var invoiceIds = invoices.Select(i => i.Id).ToArray();
            if (invoices.Any(i => i.PaidAmount != 0 || i.Status is InvoiceStatus.Paid or InvoiceStatus.PartiallyPaid) ||
                await db.PaymentTransactions.AnyAsync(p => invoiceIds.Contains(p.InvoiceId), ct))
                return Result.Failure("FDI_044", "Lần khám đã phát sinh thanh toán; không được xóa chỉ định dịch vụ.");
            // DL-139/145: remove only this assignment's live invoice snapshot; preserve cancelled history.
            foreach (var invoice in invoices.Where(i => i.Status != InvoiceStatus.Cancelled))
            {
                var items = invoice.Items.Where(i => i.ItemType == "Service" && i.SourceVisitServiceId == row.Id).ToArray();
                foreach (var item in items) { invoice.Items.Remove(item); db.InvoiceItems.Remove(item); }
                invoice.TotalAmount = invoice.Items.Sum(i => i.TotalAmount);
            }
            db.AuditLogs.Add(new AuditLog { Action = "VisitServiceDeleted", EntityType = "VisitService", EntityId = row.Id,
                UserId = actorId, IpAddress = ip, CreatedAt = DateTime.UtcNow,
                Detail = JsonSerializer.Serialize(new { row.VisitId, row.ServiceId, assignmentId = row.Id, row.ServiceName,
                    row.UnitPrice, row.Quantity, row.ToothNumber, row.Surface, reason = request.Reason }) });
            visit.UpdatedAt = DateTime.UtcNow;
            db.VisitServices.Remove(row);
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); committed = true;
            return Result.Success();
        }
        catch (Exception error) when (IsConflict(error))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result.Failure("FDI_045", "Hồ sơ vừa thay đổi đồng thời; tải lại trước khi thực hiện tiếp.");
        }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static bool IsConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException || current is PostgresException { SqlState: "40001" or "40P01" }) return true;
        return false;
    }
}
