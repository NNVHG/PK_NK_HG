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

public sealed class DentalImageRepository(DentalDbContext db, IDentalImageStorage storage) : IDentalImageRepository
{
    private static readonly Error Closed = new("IMG_002", "Chỉ gắn ảnh vào ca đang mở hoặc đính chính hợp lệ, chưa thu tiền. Ảnh nhận muộn có thể lưu riêng theo bệnh nhân.");
    private static readonly Error Limit = new("IMG_008", "Ca khám đã đạt hạn mức 20 ảnh, 3 ảnh mỗi răng hoặc 100MB. Tải lại danh sách để kiểm tra.");
    private async Task<bool> Actor(int id, string role, CancellationToken ct) => id > 0 && await db.Users.AnyAsync(x => x.UserId == id && x.IsActive && x.Role.RoleCode == role, ct);
    private async Task<bool> Patient(int id, int actor, string role, CancellationToken ct) =>
        await Actor(actor, role, ct) && await db.Patients.AnyAsync(x => x.PatientId == id && x.IsActive && (role != RoleCodes.Patient || x.UserId == actor), ct);
    private static bool CanRead(string role) => role is RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Receptionist or RoleCodes.Patient;
    private static bool CanUpload(string role) => role is RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Assistant;
    private IQueryable<Visit> Targets(int patient, int actor, string role) => db.Visits.Where(v => v.PatientId == patient && !v.IsLocked &&
        (v.Status == VisitStatuses.Created || v.Status == VisitStatuses.InProgress || v.Status == VisitStatuses.Correcting && role != RoleCodes.Assistant) &&
        (role != RoleCodes.Dentist || v.DentistId == actor) &&
        !db.Invoices.Any(i => i.VisitId == v.VisitId && (i.PaidAmount > 0 || i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.PartiallyRefunded || i.Status == InvoiceStatus.Refunded ||
            db.PaymentTransactions.Any(p => p.InvoiceId == i.Id && p.Amount > 0))));
    private async Task<bool> HasCapacity(int visit, int? tooth, long bytes, CancellationToken ct)
    {
        var active = db.DentalImages.Where(x => x.VisitId == visit && !x.IsDeleted);
        return await active.CountAsync(ct) < 20 && (tooth is null || await active.CountAsync(x => x.ToothNumber == tooth, ct) < 3) &&
            (await db.DentalImages.Where(x => x.VisitId == visit).SumAsync(x => (long?)x.SizeBytes, ct) ?? 0) + bytes <= 100L * 1024 * 1024;
    }
    private static DentalImageDto Dto(DentalImage x, string? uploadedBy = null) => new(x.Id, x.PatientId, x.VisitId, x.ToothNumber, x.Category.ToString(), x.OriginalFileName, x.ContentType, x.SizeBytes, x.Width, x.Height, x.CreatedAt, x.Version, x.Notes, uploadedBy);
    public async Task<Result<StoredDentalImage>> FileAsync(int id, int actor, string role, CancellationToken ct)
    {
        if (!CanRead(role)) return Result<StoredDentalImage>.Failure(Error.Forbidden);
        if (!await Actor(actor, role, ct)) return Result<StoredDentalImage>.Failure(Error.NotFound);
        var file = await (from image in db.DentalImages.AsNoTracking()
            join patient in db.Patients on image.PatientId equals patient.PatientId
            where image.Id == id && !image.IsDeleted && patient.IsActive && (role != RoleCodes.Patient || patient.UserId == actor)
            select new StoredDentalImage(image.StoragePath, image.ContentType, image.SizeBytes)).SingleOrDefaultAsync(ct);
        return file is null ? Result<StoredDentalImage>.Failure(Error.NotFound) : Result<StoredDentalImage>.Success(file);
    }
    public async Task<Result<ImageVisitTarget[]>> TargetsAsync(int patient, int actor, string role, CancellationToken ct)
    {
        if (!CanUpload(role)) return Result<ImageVisitTarget[]>.Failure(Error.Forbidden);
        if (!await Patient(patient, actor, role, ct)) return Result<ImageVisitTarget[]>.Failure(Error.NotFound);
        return Result<ImageVisitTarget[]>.Success(await Targets(patient, actor, role).OrderByDescending(x => x.VisitId).Take(100).Select(x => new ImageVisitTarget(x.VisitId, x.Status)).ToArrayAsync(ct));
    }
    public async Task<Result<DentalImagePage>> ListAsync(int patient, int? visit, int? tooth, int beforeId, int actor, string role, CancellationToken ct)
    {
        if (!CanRead(role)) return Result<DentalImagePage>.Failure(Error.Forbidden);
        if (!await Patient(patient, actor, role, ct)) return Result<DentalImagePage>.Failure(Error.NotFound);
        var rows = await db.DentalImages.AsNoTracking().Where(x => x.PatientId == patient && !x.IsDeleted &&
            (visit == null || x.VisitId == visit) && (tooth == null || x.ToothNumber == tooth) && (beforeId == 0 || x.Id < beforeId))
            .OrderByDescending(x => x.Id).Take(21).Select(image => new { Image = image, Name = db.Users.Where(u => u.UserId == image.UploadedByUserId).Select(u => u.FullName).FirstOrDefault() }).ToArrayAsync(ct);
        return Result<DentalImagePage>.Success(new(rows.Take(20).Select(x => Dto(x.Image, x.Name)).ToArray(), rows.Length > 20));
    }
    public async Task<Result<DentalImageDto>> UploadAsync(DentalImageRequest request, string name, string type, byte[] bytes, int width, int height, int actor, string role, string? ip, CancellationToken ct)
    {
        if (!CanUpload(role)) return Result<DentalImageDto>.Failure(Error.Forbidden);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false; string? key = null;
        try
        {
            if (!await Patient(request.PatientId, actor, role, ct)) return Result<DentalImageDto>.Failure(Error.NotFound);
            if (request.VisitId is int visit)
            {
                if (!await Targets(request.PatientId, actor, role).AnyAsync(x => x.VisitId == visit, ct)) return Result<DentalImageDto>.Failure(Closed);
                if (!await HasCapacity(visit, request.ToothNumber, bytes.Length, ct)) return Result<DentalImageDto>.Failure(Limit);
            }
            key = $"{request.PatientId}/{Guid.NewGuid():N}" + (type == "image/jpeg" ? ".jpg" : type == "image/png" ? ".png" : ".webp");
            await storage.WriteAsync(key, bytes, ct);
            var image = new DentalImage { PatientId = request.PatientId, VisitId = request.VisitId, ToothNumber = request.ToothNumber, Category = Enum.Parse<DentalImageCategory>(request.Category), Notes = request.Notes?.Trim(),
                OriginalFileName = name, StoragePath = key, ContentType = type, SizeBytes = bytes.Length, Width = width, Height = height, UploadedByUserId = actor, CreatedAt = DateTime.UtcNow };
            db.DentalImages.Add(image); await db.SaveChangesAsync(ct);
            Audit(image, actor, "MOD_IMG_UPLOADED", ip); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<DentalImageDto>.Success(Dto(image));
        }
        catch (Exception e) when (Conflict(e)) { return Result<DentalImageDto>.Failure(Error.Conflict); }
        finally
        {
            if (!committed)
            {
                db.ChangeTracker.Clear();
                if (key is not null) await storage.RemoveAsync(key);
            }
        }
    }
    public async Task<Result<DentalImageDto>> AttachAsync(int id, AttachDentalImageRequest request, int actor, string role, string? ip, CancellationToken ct)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist)) return Result<DentalImageDto>.Failure(Error.Forbidden);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var committed = false;
        try
        {
            var image = await db.DentalImages.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (image is null || !await Patient(image.PatientId, actor, role, ct)) return Result<DentalImageDto>.Failure(Error.NotFound);
            if (image.VisitId is not null || image.Version != request.ExpectedVersion || image.Version == int.MaxValue) return Result<DentalImageDto>.Failure(Error.Conflict);
            if (!await Targets(image.PatientId, actor, role).AnyAsync(x => x.VisitId == request.VisitId, ct)) return Result<DentalImageDto>.Failure(Closed);
            if (!await HasCapacity(request.VisitId, image.ToothNumber, image.SizeBytes, ct)) return Result<DentalImageDto>.Failure(Limit);
            image.VisitId = request.VisitId; image.Version++;
            Audit(image, actor, "MOD_IMG_VISIT_ATTACHED", ip); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<DentalImageDto>.Success(Dto(image));
        }
        catch (Exception e) when (Conflict(e)) { return Result<DentalImageDto>.Failure(Error.Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private void Audit(DentalImage image, int actor, string action, string? ip) => db.AuditLogs.Add(new AuditLog { UserId = actor, Action = action, EntityType = "DentalImage", EntityId = image.Id,
        CreatedAt = DateTime.UtcNow, IpAddress = ip, Detail = JsonSerializer.Serialize(new { image.VisitId, image.ToothNumber, image.Category, image.Version, image.SizeBytes }) });
    private static bool Conflict(Exception error)
    {
        for (Exception? e = error; e is not null; e = e.InnerException)
            if (e is DbUpdateConcurrencyException || e is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
