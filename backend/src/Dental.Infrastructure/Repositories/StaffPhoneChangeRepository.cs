using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Staff;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class StaffPhoneChangeRepository(DentalDbContext db, VietnamClock clock) : IStaffPhoneChangeRepository
{
    private static bool Usable(User? u) => u is { IsActive: true, RequiresPhoneActivation: false } && RoleCodes.StaffRoles.Contains(u.Role.RoleCode);
    private static bool Eligible(User? u) => Usable(u) && u!.TokenVersion < int.MaxValue;
    private static string Stamp(User u) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
        u.UserId, u.Phone, u.PasswordHash, u.RoleId, u.Role.RoleCode, u.IsActive, u.TokenVersion, u.RequiresPhoneActivation, u.PhoneVerifiedAtUtc, u.UpdatedAt
    })));
    public async Task<Result<StaffPhoneChangeTarget>> PrepareAsync(int actorId, int userId, string newPhone, string? ip, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking().Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == actorId, ct);
        if (!Usable(actor) || actor!.Role.RoleCode != RoleCodes.Admin) return Result<StaffPhoneChangeTarget>.Failure(Error.Forbidden);
        var user = await db.Users.AsNoTracking().Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null || !RoleCodes.StaffRoles.Contains(user.Role.RoleCode)) return Result<StaffPhoneChangeTarget>.Failure(Error.NotFound);
        if (!Eligible(user) || user.Phone == newPhone) return Result<StaffPhoneChangeTarget>.Failure(Error.Conflict);
        if (await db.Users.AnyAsync(u => !u.LoginByCitizenId && u.Phone == newPhone, ct)) return Result<StaffPhoneChangeTarget>.Failure(Error.StaffPhoneExists);
        // Record the request without storing either phone, OTP or proof. No credential is changed here.
        db.AuditLogs.Add(new AuditLog { UserId = actorId, EntityType = "User", EntityId = userId, Action = "MOD_AUTH_STAFF_PHONE_REQUESTED",
            Detail = JsonSerializer.Serialize(new { decision = user.PhoneVerifiedAtUtc.HasValue ? "DL-332" : "DL-333", phoneUnchanged = true }), CreatedAt = clock.UtcNow, IpAddress = ip });
        await db.SaveChangesAsync(ct);
        return Result<StaffPhoneChangeTarget>.Success(new(actorId, userId, newPhone, Stamp(actor), Stamp(user)));
    }
    public async Task<bool> CanReadAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == userId, ct);
        return Eligible(user);
    }
    public async Task<Result<bool>> CompleteAsync(StaffPhoneChangeTarget target, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            var actor = await db.Users.AsNoTracking().Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == target.ActorId, ct);
            var user = await db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == target.UserId, ct);
            if (!Usable(actor) || actor!.Role.RoleCode != RoleCodes.Admin || !Eligible(user) ||
                Stamp(actor) != target.ActorStamp || Stamp(user!) != target.UserStamp)
                return Result<bool>.Failure(StaffPhoneChangeService.Invalid);
            if (await db.Users.AnyAsync(u => !u.LoginByCitizenId && u.Phone == target.NewPhone && u.UserId != target.UserId, ct)) return Result<bool>.Failure(Error.StaffPhoneExists);
            var wasVerified = user!.PhoneVerifiedAtUtc.HasValue;
            user.Phone = target.NewPhone;
            user.PhoneVerifiedAtUtc = clock.UtcNow;
            user.TokenVersion++;
            user.UpdatedAt = clock.UtcNow;
            db.AuditLogs.Add(new AuditLog { UserId = user.UserId, EntityType = "User", EntityId = user.UserId, Action = "MOD_AUTH_STAFF_PHONE_CHANGED",
                Detail = JsonSerializer.Serialize(new { requestedBy = target.ActorId, phoneVerified = true, sessionsRevoked = true, passwordChanged = false, previouslyVerified = wasVerified, decision = wasVerified ? "DL-332" : "DL-333" }),
                CreatedAt = clock.UtcNow, IpAddress = ip });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct); committed = true;
            return Result<bool>.Success(true);
        }
        catch (Exception e) when (IsConflict(e)) { return Result<bool>.Failure(Error.Conflict); }
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
