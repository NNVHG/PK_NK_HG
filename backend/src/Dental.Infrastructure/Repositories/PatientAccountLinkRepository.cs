using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_PAT;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class PatientAccountLinkRepository(DentalDbContext db) : IPatientAccountLinkRepository
{
    private Task<bool> Actor(int actor, string role, CancellationToken ct) =>
        role is RoleCodes.Admin or RoleCodes.Receptionist
            ? db.Users.AnyAsync(u => u.UserId == actor && u.IsActive && u.Role.RoleCode == role, ct)
            : Task.FromResult(false);
    private Task<User?> Account(string phone, CancellationToken ct) => db.Users.AsNoTracking()
        .SingleOrDefaultAsync(u => u.Phone == phone && u.IsActive && u.Role.RoleCode == RoleCodes.Patient, ct);
    // Only an optimistic review fingerprint; JWT and DB authorization are checked separately on every request.
    private static string Stamp(Patient p, User u) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
        p.PatientId, patientName = p.FullName, patientBirth = p.DateOfBirth, patientPhone = p.Phone,
        patientUpdated = p.UpdatedAt, p.CitizenIdVersion, p.UserId,
        accountId = u.UserId, accountName = u.FullName, accountPhone = u.Phone, accountBirth = u.DateOfBirth,
        accountUpdated = u.UpdatedAt, u.RoleId, u.TokenVersion
    })));
    public async Task<Result<PatientAccountLinkContext>> ReadAsync(int patientId, int actor, string role, CancellationToken ct)
    {
        if (!await Actor(actor, role, ct)) return Result<PatientAccountLinkContext>.Failure(Error.Forbidden);
        var p = await db.Patients.AsNoTracking().SingleOrDefaultAsync(p => p.PatientId == patientId && p.IsActive, ct);
        if (p is null) return Result<PatientAccountLinkContext>.Failure(Error.NotFound);
        var u = p.UserId is null ? await Account(p.Phone, ct) : null;
        if (u is not null && await db.Patients.AnyAsync(other => other.UserId == u.UserId, ct))
            u = null;
        return Result<PatientAccountLinkContext>.Success(new(p.PatientId, p.FullName, p.DateOfBirth, p.Phone, p.UserId.HasValue,
            u is null ? null : new(u.UserId, u.FullName, u.Phone, u.DateOfBirth), u is null ? null : Stamp(p, u)));
    }
    public async Task<Result<PatientAccountLinked>> LinkAsync(int patientId, int actor, string role, LinkPatientAccountRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (!await Actor(actor, role, ct)) return Result<PatientAccountLinked>.Failure(Error.Forbidden);
            var p = await db.Patients.SingleOrDefaultAsync(p => p.PatientId == patientId && p.IsActive, ct);
            if (p is null) return Result<PatientAccountLinked>.Failure(Error.NotFound);
            if (p.UserId.HasValue) return Conflict();
            var u = await Account(p.Phone, ct);
            if (u is null || u.UserId != request.AccountUserId || Stamp(p, u) != request.ReviewStamp) return Conflict();
            if (await db.Patients.AnyAsync(other => other.UserId == u.UserId && other.PatientId != patientId, ct)) return Conflict();
            p.UserId = u.UserId;
            p.UpdatedAt = DateTime.UtcNow;
            db.AuditLogs.Add(new AuditLog { UserId = actor, EntityType = "Patient", EntityId = p.PatientId,
                Action = "MOD_PAT_FAMILY_ACCOUNT_LINKED", CreatedAt = DateTime.UtcNow, IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { accountUserId = u.UserId, confirmedAtCounter = true, decision = "DL-325" }) });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct); committed = true;
            return Result<PatientAccountLinked>.Success(new(p.PatientId, u.UserId));
        }
        catch (Exception e) when (IsConflict(e)) { return Conflict(); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static Result<PatientAccountLinked> Conflict() => Result<PatientAccountLinked>.Failure(new Error("PAT_ACCOUNT_CONFLICT", "Hồ sơ hoặc tài khoản vừa thay đổi hoặc không đủ điều kiện. Tải lại để đối soát."));
    private static bool IsConflict(Exception e)
    {
        for (Exception? x = e; x is not null; x = x.InnerException)
            if (x is DbUpdateConcurrencyException || x is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
