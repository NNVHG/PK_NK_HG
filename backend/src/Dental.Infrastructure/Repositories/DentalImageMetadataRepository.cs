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

public sealed class DentalImageMetadataRepository(DentalDbContext db, VietnamClock clock) : IDentalImageMetadataRepository
{
    private static readonly Error Conflict = new("IMG_METADATA_CONFLICT", "Ảnh hoặc ca vừa thay đổi. Tải lại để đối chiếu trước khi lưu.");
    private static readonly Error Locked = new("IMG_METADATA_LOCKED", "Chỉ sửa thông tin ảnh trong ca đang mở hoặc đính chính hợp lệ, chưa khóa/chưa thu tiền.");
    private static readonly Error Limit = new("IMG_METADATA_LIMIT", "Răng đích đã có đủ 3 ảnh trong lần khám. Chọn răng khác hoặc bỏ liên kết răng.");

    private async Task<ImageClinicalState?> Load(int id, int actor, string role, bool tracking, CancellationToken ct)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist) ||
            !await db.Users.AnyAsync(u => u.UserId == actor && u.IsActive && u.Role.RoleCode == role, ct)) return null;
        var rows = tracking ? db.DentalImages : db.DentalImages.AsNoTracking();
        var image = await rows.SingleOrDefaultAsync(i => i.Id == id && !i.IsDeleted && db.Patients.Any(p => p.PatientId == i.PatientId && p.IsActive), ct);
        if (image is null) return null;
        var visit = image.VisitId is int visitId ? await db.Visits.AsNoTracking().SingleOrDefaultAsync(v => v.VisitId == visitId && v.PatientId == image.PatientId, ct) : null;
        var payments = visit is not null && await db.Invoices.AnyAsync(i => i.VisitId == visit.VisitId &&
            (i.PaidAmount != 0 || i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.PartiallyPaid ||
             i.Status == InvoiceStatus.PartiallyRefunded || i.Status == InvoiceStatus.Refunded || db.PaymentTransactions.Any(p => p.InvoiceId == i.Id)), ct);
        return new(image, visit, payments);
    }

    private static DentalImageMetadataContext Context(ImageClinicalState state, int actor, string role)
    {
        var i = state.Image;
        return new(i.Id, i.PatientId, i.VisitId, i.Version, i.OriginalFileName, i.Category.ToString(), i.ToothNumber, i.Notes,
            ImageClinicalRules.Owns(state, actor, role) && ImageClinicalRules.MayReplace(state));
    }

    public async Task<Result<DentalImageMetadataContext>> ReadAsync(int image, int actor, string role, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var state = await Load(image, actor, role, false, ct);
        if (state is null) return Result<DentalImageMetadataContext>.Failure(Error.NotFound);
        var result = Context(state, actor, role);
        await tx.CommitAsync(ct);
        return Result<DentalImageMetadataContext>.Success(result);
    }

    public async Task<Result<DentalImageMetadataContext>> UpdateAsync(int image, int actor, string role, UpdateDentalImageMetadataRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            var state = await Load(image, actor, role, true, ct);
            if (state is null || !ImageClinicalRules.Owns(state, actor, role)) return Result<DentalImageMetadataContext>.Failure(Error.NotFound);
            var i = state.Image;
            if (i.Version != request.ExpectedVersion) return Result<DentalImageMetadataContext>.Failure(Conflict);
            if (!ImageClinicalRules.MayReplace(state)) return Result<DentalImageMetadataContext>.Failure(Locked);
            if (i.VisitId is int visit && request.ToothNumber is int tooth &&
                await db.DentalImages.CountAsync(other => !other.IsDeleted && other.VisitId == visit && other.Id != i.Id && other.ToothNumber == tooth, ct) >= 3)
                return Result<DentalImageMetadataContext>.Failure(Limit);

            var previousCategory = i.Category.ToString();
            var previousTooth = i.ToothNumber;
            var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            var notesChanged = i.Notes != notes;
            i.Category = Enum.Parse<DentalImageCategory>(request.Category);
            i.ToothNumber = request.ToothNumber;
            i.Notes = notes;
            i.Version++;
            db.AuditLogs.Add(new AuditLog
            {
                UserId = actor,
                Action = "MOD_IMG_METADATA_UPDATED",
                EntityType = "DentalImage",
                EntityId = i.Id,
                CreatedAt = clock.UtcNow,
                IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { i.VisitId, i.Version, previousCategory, category = i.Category.ToString(), previousTooth, i.ToothNumber, notesChanged })
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            committed = true;
            return Result<DentalImageMetadataContext>.Success(Context(state, actor, role));
        }
        catch (Exception e) when (IsConflict(e)) { return Result<DentalImageMetadataContext>.Failure(Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }

    private static bool IsConflict(Exception e)
    {
        for (Exception? current = e; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException || current is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
