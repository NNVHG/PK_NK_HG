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

public sealed class PatientActivationRepository(DentalDbContext db, VietnamClock clock) : IPatientActivationRepository
{
    private IQueryable<User> EligibleStaff() => db.Users.Include(u => u.Role).Where(u => u.IsActive && u.RequiresPhoneActivation &&
        RoleCodes.StaffRoles.Contains(u.Role.RoleCode));
    private IQueryable<User> EligiblePatient() => db.Users.Include(u => u.Role).Where(u => u.IsActive && u.RequiresPhoneActivation &&
        u.LoginByCitizenId && u.Role.RoleCode == RoleCodes.Patient && db.Patients.Any(p => p.UserId == u.UserId && p.IsActive && p.CitizenIdHash != null));
    private static string Stamp(User u) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
        u.UserId, u.Phone, u.PasswordHash, u.TokenVersion, u.RoleId, u.Role.RoleCode, u.IsActive, u.RequiresPhoneActivation, u.LoginByCitizenId, u.UpdatedAt
    })));
    public async Task<ActivationAccount?> FindPendingAsync(string identifier, CancellationToken ct)
    {
        User? user;
        var patient = identifier.Length == 12;
        if (patient)
        {
            var hash = Dental.Application.Features.MOD_PAT.CitizenIdIdentity.Hash(identifier);
            var matches = await EligiblePatient().AsNoTracking().Where(u => db.Patients.Any(p => p.UserId == u.UserId && p.CitizenIdHash == hash))
                .Take(2).ToListAsync(ct);
            user = matches.Count == 1 ? matches[0] : null;
        }
        else user = await EligibleStaff().AsNoTracking().SingleOrDefaultAsync(u => u.Phone == identifier, ct);
        return user is null ? null : new(new(user.UserId, Stamp(user), patient), user.PasswordHash, user.Phone);
    }
    public async Task<bool> IsCurrentAsync(ActivationTarget target, CancellationToken ct)
    {
        var user = await EligibleFor(target).AsNoTracking().SingleOrDefaultAsync(u => u.UserId == target.UserId, ct);
        return user is not null && Stamp(user) == target.Stamp;
    }
    public async Task<bool> CompleteAsync(ActivationTarget target, string? passwordHash, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var committed = false;
        try
        {
            var user = await EligibleFor(target).SingleOrDefaultAsync(u => u.UserId == target.UserId, ct);
            if (user is null || user.TokenVersion == int.MaxValue || Stamp(user) != target.Stamp) return false;
            if (passwordHash is not null) user.PasswordHash = passwordHash;
            user.RequiresPhoneActivation = false; user.PhoneVerifiedAtUtc = clock.UtcNow; user.TokenVersion++; user.UpdatedAt = clock.UtcNow;
            var staff = RoleCodes.StaffRoles.Contains(user.Role.RoleCode);
            db.AuditLogs.Add(new AuditLog { UserId = user.UserId, EntityType = "User", EntityId = user.UserId,
                Action = staff ? "MOD_AUTH_STAFF_ACTIVATED" : "MOD_AUTH_PATIENT_ACTIVATED", CreatedAt = clock.UtcNow, IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { phoneVerified = true, changedPassword = passwordHash is not null, decision = staff ? "DL-331" : "DL-330" }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true; return true;
        }
        catch (Exception e) when (IsConflict(e)) { return false; }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private IQueryable<User> EligibleFor(ActivationTarget target) => target.MustChangePassword ? EligiblePatient() : EligibleStaff();
    private static bool IsConflict(Exception e)
    {
        for (Exception? x = e; x is not null; x = x.InnerException)
            if (x is DbUpdateConcurrencyException || x is PostgresException { SqlState: "40001" or "40P01" }) return true;
        return false;
    }
}
