using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Dental.Infrastructure.Repositories;

public sealed class DentalImageClinicalRepository(DentalDbContext db, VietnamClock clock) : IDentalImageClinicalRepository
{
    private static readonly Error Conflict = new("IMG_CLINICAL_CONFLICT", "Ảnh hoặc ca khám vừa thay đổi. Tải lại trước khi xác nhận.");
    private static readonly Error Locked = new("IMG_CLINICAL_LOCKED", "Không được sửa theo trạng thái hiện tại. Ca hoàn tất chỉ cho thêm ghi chú nối tiếp.");
    private async Task<ImageClinicalState?> LoadAsync(int id, int actor, string role, bool tracking, CancellationToken ct)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Receptionist or RoleCodes.Patient) ||
            !await db.Users.AnyAsync(u => u.UserId == actor && u.IsActive && u.Role.RoleCode == role, ct)) return null;
        var query = tracking ? db.DentalImages : db.DentalImages.AsNoTracking();
        var image = await query.SingleOrDefaultAsync(i => i.Id == id && !i.IsDeleted && db.Patients.Any(p => p.PatientId == i.PatientId && p.IsActive && (role != RoleCodes.Patient || p.UserId == actor)), ct);
        if (image is null) return null;
        var visit = image.VisitId is int visitId ? await db.Visits.AsNoTracking().SingleOrDefaultAsync(v => v.VisitId == visitId && v.PatientId == image.PatientId, ct) : null;
        var paid = visit is not null && await db.Invoices.AnyAsync(i => i.VisitId == visit.VisitId &&
            (i.PaidAmount != 0 || i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.PartiallyRefunded || i.Status == InvoiceStatus.Refunded || db.PaymentTransactions.Any(p => p.InvoiceId == i.Id)), ct);
        return new(image, visit, paid);
    }
    private async Task<ImageClinicalContext> ContextAsync(ImageClinicalState state, int actor, string role, CancellationToken ct)
    {
        var i = state.Image;
        var author = i.ClinicalUpdatedByUserId is int user ? await db.Users.Where(u => u.UserId == user).Select(u => u.FullName).SingleOrDefaultAsync(ct) : null;
        var owns = ImageClinicalRules.Owns(state, actor, role);
        return new(i.Id, i.PatientId, i.VisitId, i.Version, i.ClinicalNotes, i.DoctorDiagnosis, i.ClinicalUpdatedByUserId, author, i.ClinicalUpdatedAt,
            owns && ImageClinicalRules.MayReplace(state), owns && ImageClinicalRules.MayAppend(state));
    }
    public async Task<Result<ImageClinicalContext>> ReadAsync(int image, int actor, string role, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var state = await LoadAsync(image, actor, role, false, ct);
        if (state is null) return Result<ImageClinicalContext>.Failure(Error.NotFound);
        var context = await ContextAsync(state, actor, role, ct); await tx.CommitAsync(ct);
        return Result<ImageClinicalContext>.Success(context);
    }
    public async Task<Result<PagedResult<ImageClinicalAddendumDto>>> HistoryAsync(int image, int actor, string role, int page, int pageSize, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (await LoadAsync(image, actor, role, false, ct) is null) return Result<PagedResult<ImageClinicalAddendumDto>>.Failure(Error.NotFound);
        var rows = db.DentalImageClinicalAddenda.AsNoTracking().Where(a => a.DentalImageId == image);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new ImageClinicalAddendumDto(a.Id, a.DentalImageId, a.ImageVersion, a.CreatedByUserId, a.AuthorName, a.AuthorRole, a.CreatedAt, a.Note)).ToArrayAsync(ct);
        await tx.CommitAsync(ct);
        return Result<PagedResult<ImageClinicalAddendumDto>>.Success(PagedResult<ImageClinicalAddendumDto>.Create(items, count, page, pageSize));
    }
    public async Task<Result<ImageClinicalContext>> ReplaceAsync(int image, int actor, string role, ImageClinicalWriteRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var committed = false;
        try
        {
            var state = await LoadAsync(image, actor, role, true, ct);
            if (state is null || !ImageClinicalRules.Owns(state, actor, role)) return Result<ImageClinicalContext>.Failure(Error.NotFound);
            if (state.Image.Version != request.ExpectedVersion) return Result<ImageClinicalContext>.Failure(Conflict);
            if (!ImageClinicalRules.MayReplace(state)) return Result<ImageClinicalContext>.Failure(Locked);
            var i = state.Image; i.ClinicalNotes = Normalize(request.ClinicalNotes); i.DoctorDiagnosis = Normalize(request.DoctorDiagnosis);
            i.ClinicalUpdatedByUserId = actor; i.ClinicalUpdatedAt = clock.UtcNow; i.Version++;
            Audit("MOD_IMG_CLINICAL_UPDATED", i, actor, ip, null);
            await db.SaveChangesAsync(ct);
            var context = await ContextAsync(state, actor, role, ct); await tx.CommitAsync(ct); committed = true;
            return Result<ImageClinicalContext>.Success(context);
        }
        catch (Exception e) when (IsConflict(e)) { return Result<ImageClinicalContext>.Failure(Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    public async Task<Result<ImageClinicalAddendumDto>> AppendAsync(int image, int actor, string role, ImageClinicalAppendRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var committed = false;
        try
        {
            var state = await LoadAsync(image, actor, role, true, ct);
            if (state is null || !ImageClinicalRules.Owns(state, actor, role)) return Result<ImageClinicalAddendumDto>.Failure(Error.NotFound);
            if (state.Visit?.Status != VisitStatuses.Completed) return Result<ImageClinicalAddendumDto>.Failure(Locked);
            var previous = await db.DentalImageClinicalAddenda.AsNoTracking().SingleOrDefaultAsync(a => a.RequestId == request.RequestId, ct);
            if (previous is not null)
            {
                if (previous.DentalImageId != image || previous.CreatedByUserId != actor || previous.ImageVersion - 1 != request.ExpectedVersion || previous.Note != request.Note.Trim()) return Result<ImageClinicalAddendumDto>.Failure(Conflict);
                await tx.CommitAsync(ct); committed = true; return Result<ImageClinicalAddendumDto>.Success(Dto(previous));
            }
            if (state.Image.Version != request.ExpectedVersion) return Result<ImageClinicalAddendumDto>.Failure(Conflict);
            if (!ImageClinicalRules.MayAppend(state)) return Result<ImageClinicalAddendumDto>.Failure(Locked);
            var author = await db.Users.Where(u => u.UserId == actor).Select(u => u.FullName).SingleAsync(ct);
            state.Image.Version++;
            var entry = new DentalImageClinicalAddendum
            {
                DentalImageId = image,
                CreatedByUserId = actor,
                AuthorName = author,
                AuthorRole = role,
                Note = request.Note.Trim(),
                CreatedAt = clock.UtcNow,
                RequestId = request.RequestId,
                ImageVersion = state.Image.Version
            };
            db.DentalImageClinicalAddenda.Add(entry); Audit("MOD_IMG_CLINICAL_APPENDED", state.Image, actor, ip, request.RequestId);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<ImageClinicalAddendumDto>.Success(Dto(entry));
        }
        catch (Exception e) when (IsConflict(e)) { return Result<ImageClinicalAddendumDto>.Failure(Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ImageClinicalAddendumDto Dto(DentalImageClinicalAddendum a) => new(a.Id, a.DentalImageId, a.ImageVersion, a.CreatedByUserId, a.AuthorName, a.AuthorRole, a.CreatedAt, a.Note);
    private void Audit(string action, DentalImage image, int actor, string? ip, Guid? requestId) => db.AuditLogs.Add(new AuditLog
    {
        UserId = actor,
        Action = action,
        EntityType = "DentalImage",
        EntityId = image.Id,
        CreatedAt = clock.UtcNow,
        IpAddress = ip,
        Detail = JsonSerializer.Serialize(new { image.VisitId, image.Version, requestId })
    });
    private static bool IsConflict(Exception e)
    { for (Exception? current = e; current is not null; current = current.InnerException) if (current is DbUpdateConcurrencyException || current is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true; return false; }
}
