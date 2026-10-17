using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Dental.Infrastructure.Repositories;
public sealed class DoctorSignatureRepository(DentalDbContext db) : IDoctorSignatureRepository
{
    private async Task<string?> AuthorizedDoctor(int doctorId, int actorId, string role, CancellationToken ct)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist) || role == RoleCodes.Dentist && actorId != doctorId ||
            !await db.Users.AnyAsync(u => u.UserId == actorId && u.IsActive && u.Role.RoleCode == role, ct)) return null;
        return await db.Users.Where(u => u.UserId == doctorId && u.IsActive && u.Role.RoleCode == RoleCodes.Dentist).Select(u => u.FullName).SingleOrDefaultAsync(ct);
    }
    private static DoctorSignatureStatus Status(int id, string name, DoctorSignature? image) =>
        new(id, name, image?.Version ?? 0, image is null ? null : $"/api/doctors/{id}/signature/file?version={image.Version}", image?.CreatedAt);
    public async Task<Result<DoctorSignatureStatus>> ReadAsync(int doctorId, int actorId, string role, CancellationToken ct)
    {
        if (await AuthorizedDoctor(doctorId, actorId, role, ct) is not { } name) return Result<DoctorSignatureStatus>.Failure(Error.NotFound);
        var image = await db.DoctorSignatures.AsNoTracking().Where(x => x.DoctorId == doctorId).OrderByDescending(x => x.Version)
            .Select(x => new DoctorSignature { Version = x.Version, CreatedAt = x.CreatedAt }).FirstOrDefaultAsync(ct);
        return Result<DoctorSignatureStatus>.Success(Status(doctorId, name, image));
    }
    public async Task<Result<byte[]>> FileAsync(int doctorId, int version, int actorId, string role, CancellationToken ct)
    {
        if (await AuthorizedDoctor(doctorId, actorId, role, ct) is null) return Result<byte[]>.Failure(Error.NotFound);
        var bytes = await db.DoctorSignatures.AsNoTracking().Where(x => x.DoctorId == doctorId && x.Version == version).Select(x => x.ImagePng).SingleOrDefaultAsync(ct);
        return bytes is null ? Result<byte[]>.Failure(Error.NotFound) : Result<byte[]>.Success(bytes);
    }
    public async Task<Result<DoctorSignatureStatus>> SaveAsync(int doctorId, int actorId, string role, int expectedVersion, byte[] image, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var committed = false;
        try
        {
            if (await AuthorizedDoctor(doctorId, actorId, role, ct) is not { } name) return Result<DoctorSignatureStatus>.Failure(Error.NotFound);
            var version = await db.DoctorSignatures.Where(x => x.DoctorId == doctorId).MaxAsync(x => (int?)x.Version, ct) ?? 0;
            if (version != expectedVersion || version == int.MaxValue) return Result<DoctorSignatureStatus>.Failure(Error.Conflict);
            var next = new DoctorSignature { DoctorId = doctorId, Version = version + 1, ImagePng = image, CreatedAt = DateTime.UtcNow, UpdatedByUserId = actorId };
            db.DoctorSignatures.Add(next);
            db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "MOD_AUTH_DOCTOR_SIGNATURE_ADDED", EntityType = "User", EntityId = doctorId, CreatedAt = next.CreatedAt, IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { version = next.Version, size = image.Length }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<DoctorSignatureStatus>.Success(Status(doctorId, name, next));
        }
        catch (Exception e) when (Conflict(e)) { return Result<DoctorSignatureStatus>.Failure(Error.Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static bool Conflict(Exception e)
    {
        for (Exception? x = e; x is not null; x = x.InnerException)
            if (x is DbUpdateConcurrencyException || x is PostgresException { SqlState: "23505" or "40001" or "40P01" }) return true;
        return false;
    }
}
