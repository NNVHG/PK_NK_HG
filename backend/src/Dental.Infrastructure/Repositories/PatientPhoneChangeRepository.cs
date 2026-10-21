using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class PatientPhoneChangeRepository(DentalDbContext db, VietnamClock clock) : IPatientPhoneChangeRepository
{
    private static bool Eligible(User? user) => user is { IsActive: true, RequiresPhoneActivation: false } &&
        user.Role.RoleCode == RoleCodes.Patient && user.TokenVersion < int.MaxValue;
    private static string Stamp(User user, string newPhone, IReadOnlyList<Patient> profiles) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new {
            user.UserId, user.Phone, user.PasswordHash, user.RoleId, user.Role.RoleCode, user.IsActive,
            user.RequiresPhoneActivation, user.PhoneVerifiedAtUtc, user.TokenVersion, user.UpdatedAt, newPhone,
            profiles = profiles.Select(p => new { p.PatientId, p.UserId, p.Phone, p.FullName, p.DateOfBirth, p.IsActive, p.UpdatedAt })
        })));
    public async Task<Result<PatientPhonePreview>> PreviewAsync(int userId, string newPhone, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == userId, ct);
        if (!Eligible(user)) return Result<PatientPhonePreview>.Failure(Error.Forbidden);
        if (user!.Phone == newPhone || await db.Users.AnyAsync(u => u.Phone == newPhone, ct))
            return Result<PatientPhonePreview>.Failure(PatientPhoneChangeService.NumberUnavailable);
        var profiles = await db.Patients.AsNoTracking().Where(p => p.UserId == userId).OrderBy(p => p.PatientId).ToListAsync(ct);
        return Result<PatientPhonePreview>.Success(new(user.Phone, newPhone, Stamp(user, newPhone, profiles),
            profiles.Select(p => new PatientPhoneScopeItem(p.PatientId, p.FullName, p.DateOfBirth, p.IsActive, p.Phone == user.Phone)).ToList()));
    }
    public async Task<Result<PatientPhoneTarget>> PrepareAsync(int userId, PatientPhoneRequest request, string? ip, CancellationToken ct)
    {
        var preview = await PreviewAsync(userId, request.NewPhone, ct);
        if (preview.IsFailure) return Result<PatientPhoneTarget>.Failure(preview.Error);
        if (preview.Value!.ReviewStamp != request.ReviewStamp) return Result<PatientPhoneTarget>.Failure(PatientPhoneChangeService.Stale);
        db.AuditLogs.Add(new AuditLog { UserId = userId, EntityType = "User", EntityId = userId,
            Action = "MOD_AUTH_PATIENT_PHONE_REQUESTED", CreatedAt = clock.UtcNow, IpAddress = ip,
            Detail = JsonSerializer.Serialize(new { decision = "DL-334", confirmedScope = true,
                patientIds = preview.Value.Profiles.Where(p => p.WillUpdate).Select(p => p.PatientId) }) });
        await db.SaveChangesAsync(ct);
        return Result<PatientPhoneTarget>.Success(new(userId, request.NewPhone, request.ReviewStamp));
    }
    public async Task<Result<PatientPhoneCompleted>> CompleteAsync(PatientPhoneTarget target, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            var user = await db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == target.UserId, ct);
            if (!Eligible(user)) return Result<PatientPhoneCompleted>.Failure(Error.Forbidden);
            var profiles = await db.Patients.Where(p => p.UserId == target.UserId).OrderBy(p => p.PatientId).ToListAsync(ct);
            if (Stamp(user!, target.NewPhone, profiles) != target.ReviewStamp) return Result<PatientPhoneCompleted>.Failure(PatientPhoneChangeService.Stale);
            if (await db.Users.AnyAsync(u => u.Phone == target.NewPhone, ct))
                return Result<PatientPhoneCompleted>.Failure(PatientPhoneChangeService.NumberUnavailable);
            var affected = profiles.Where(p => p.Phone == user!.Phone).ToList();
            var now = clock.UtcNow;
            foreach (var profile in affected)
            {
                profile.Phone = target.NewPhone;
                profile.UpdatedAt = now;
                db.AuditLogs.Add(new AuditLog { UserId = target.UserId, EntityType = "Patient", EntityId = profile.PatientId,
                    Action = "MOD_PAT_CONTACT_PHONE_SYNCED", CreatedAt = now, IpAddress = ip,
                    Detail = JsonSerializer.Serialize(new { decision = "DL-334", confirmedByAccountOwner = true }) });
            }
            var counterRequest = await db.PatientPhoneChangeRequests.SingleOrDefaultAsync(x =>
                x.UserId == target.UserId && x.TokenVersion == user!.TokenVersion && x.CurrentPhone == user.Phone &&
                x.NewPhone == target.NewPhone && x.CompletedAtUtc == null, ct);
            if (counterRequest is not null)
            {
                counterRequest.CompletedAtUtc = now;
                db.AuditLogs.Add(new AuditLog { UserId = target.UserId, EntityType = "PatientPhoneChangeRequest",
                    Action = "MOD_AUTH_COUNTER_PHONE_CONFIRMED", CreatedAt = now, IpAddress = ip,
                    Detail = JsonSerializer.Serialize(new { decision = "DL-335", requestId = counterRequest.Id,
                        initiatedByUserId = counterRequest.CreatedByUserId, ownerUserId = target.UserId,
                        counterRequest.AppointmentId, confirmedByAccountOwner = true }) });
            }
            user!.Phone = target.NewPhone;
            user.PhoneVerifiedAtUtc = now;
            user.TokenVersion++;
            user.UpdatedAt = now;
            db.AuditLogs.Add(new AuditLog { UserId = user.UserId, EntityType = "User", EntityId = user.UserId,
                Action = "MOD_AUTH_PATIENT_PHONE_CHANGED", CreatedAt = now, IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { decision = "DL-334", sessionsRevoked = true, passwordChanged = false,
                    confirmedScope = true, patientIds = affected.Select(p => p.PatientId) }) });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct); committed = true;
            return Result<PatientPhoneCompleted>.Success(new(true, affected.Count));
        }
        catch (Exception e) when (IsConflict(e)) { return Result<PatientPhoneCompleted>.Failure(Error.Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static bool IsConflict(Exception e)
    {
        for (Exception? x = e; x is not null; x = x.InnerException)
            if (x is DbUpdateConcurrencyException || x is PostgresException { SqlState: "40001" or "40P01" } ||
                x is PostgresException { SqlState: "23505", ConstraintName: "IX_Users_Phone" }) return true;
        return false;
    }
}
