using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class DentalImageDeletionRepository(DentalDbContext db, VietnamClock clock) : IDentalImageDeletionRepository
{
    private static readonly Error Conflict = new("IMG_DELETE_CONFLICT", "Ảnh hoặc ca vừa thay đổi. Tải lại để đối chiếu trước khi xác nhận.");
    private static readonly Error Closed = new("IMG_DELETE_LOCKED", "Chỉ xóa ảnh độc lập hoặc ảnh trong ca đang khám chưa khóa, chưa có thanh toán.");

    private Task<bool> Actor(int actor, string role, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.UserId == actor && u.IsActive && u.Role.RoleCode == role, ct);

    private Task<bool> HasPayments(int visit, CancellationToken ct) => db.Invoices.AnyAsync(i => i.VisitId == visit &&
        (i.PaidAmount != 0 || i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.PartiallyPaid ||
         i.Status == InvoiceStatus.PartiallyRefunded || i.Status == InvoiceStatus.Refunded ||
         db.PaymentTransactions.Any(p => p.InvoiceId == i.Id)), ct);

    private async Task<bool> Open(DentalImage image, CancellationToken ct)
    {
        var visit = image.VisitId is int id
            ? await db.Visits.AsNoTracking().SingleOrDefaultAsync(v => v.VisitId == id && v.PatientId == image.PatientId, ct)
            : null;
        var paid = image.VisitId is int visitId && await HasPayments(visitId, ct);
        return DentalImageDeletionRules.IsOpen(image, visit, paid);
    }

    public async Task<Result<DentalImageDeletionPage>> ListAsync(int patient, int beforeId, int actor, string role, CancellationToken ct)
    {
        if (!DentalImageDeletionRules.RoleAllowed(role)) return Result<DentalImageDeletionPage>.Failure(Error.Forbidden);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await Actor(actor, role, ct) || !await db.Patients.AnyAsync(p => p.PatientId == patient && p.IsActive, ct))
            return Result<DentalImageDeletionPage>.Failure(Error.NotFound);
        var images = await db.DentalImages.AsNoTracking().Where(i => i.PatientId == patient && !i.IsDeleted &&
            (role == RoleCodes.Admin || i.UploadedByUserId == actor) && (beforeId == 0 || i.Id < beforeId))
            .OrderByDescending(i => i.Id).Take(21).ToArrayAsync(ct);
        var items = new List<DentalImageDeletionItem>();
        foreach (var image in images.Take(20))
        {
            var open = await Open(image, ct);
            items.Add(new(image.Id, image.PatientId, image.VisitId, image.Version, image.OriginalFileName,
                image.Category.ToString(), image.ToothNumber, image.CreatedAt, open, open ? null : Closed.Message));
        }
        await tx.CommitAsync(ct);
        return Result<DentalImageDeletionPage>.Success(new(items.ToArray(), images.Length > 20));
    }

    public async Task<Result<bool>> DeleteAsync(int imageId, int actor, string role, DeleteDentalImageRequest request, string? ip, CancellationToken ct)
    {
        if (!DentalImageDeletionRules.RoleAllowed(role)) return Result<bool>.Failure(Error.Forbidden);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (!await Actor(actor, role, ct)) return Result<bool>.Failure(Error.NotFound);
            var image = await db.DentalImages.SingleOrDefaultAsync(i => i.Id == imageId && !i.IsDeleted &&
                db.Patients.Any(p => p.PatientId == i.PatientId && p.IsActive), ct);
            if (image is null || !DentalImageDeletionRules.Owns(image, actor, role)) return Result<bool>.Failure(Error.NotFound);
            if (image.Version != request.ExpectedVersion) return Result<bool>.Failure(Conflict);
            if (!await Open(image, ct)) return Result<bool>.Failure(Closed);

            image.IsDeleted = true;
            image.DeletedAt = clock.UtcNow;
            image.DeletedByUserId = actor;
            image.DeleteReason = request.Reason.Trim();
            image.Version++;
            db.AuditLogs.Add(new AuditLog
            {
                UserId = actor,
                Action = "MOD_IMG_SOFT_DELETED",
                EntityType = "DentalImage",
                EntityId = image.Id,
                CreatedAt = image.DeletedAt.Value,
                IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { image.VisitId, image.Version, image.DeletedByUserId, image.DeleteReason })
            });
            // Storage is intentionally not a dependency: deletion only hides metadata (DL097/Q285a).
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            committed = true;
            return Result<bool>.Success(true);
        }
        catch (Exception e) when (IsConflict(e)) { return Result<bool>.Failure(Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }

    private static bool IsConflict(Exception e)
    {
        for (Exception? current = e; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException || current is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
