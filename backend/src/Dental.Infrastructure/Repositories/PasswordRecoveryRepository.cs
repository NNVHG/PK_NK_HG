using System.Data;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class PasswordRecoveryRepository(DentalDbContext db, IAuditLogger audit, TimeProvider clock) : IPasswordRecoveryRepository
{
    public async Task<RecoveryAccount?> FindActiveAsync(string phone, string? citizenId, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(citizenId))
        {
            var hash = Dental.Application.Features.MOD_PAT.CitizenIdIdentity.Hash(citizenId);
            var matches = await db.Patients.AsNoTracking()
                .Where(p => p.CitizenIdHash == hash && p.Phone == phone && p.User != null && p.User.IsActive &&
                            p.User.Role.RoleCode == RoleCodes.Patient)
                .Select(p => new RecoveryAccount(p.User!.UserId, p.User.PasswordHash, p.User.Phone, p.CitizenIdHash))
                .Take(2).ToListAsync(ct);
            return matches.Count == 1 ? matches[0] : null;
        }

        var staff = await db.Users.AsNoTracking()
            .Where(u => u.Phone == phone && u.IsActive && RoleCodes.StaffRoles.Contains(u.Role.RoleCode))
            .Select(u => new RecoveryAccount(u.UserId, u.PasswordHash, u.Phone, null))
            .Take(2).ToListAsync(ct);
        return staff.Count == 1 ? staff[0] : null;
    }

    public async Task<bool> ResetAsync(RecoveryAccount account, string passwordHash, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Atomic compare-and-set also invalidates a code after a normal password change, role lock or competing reset.
        var changed = await db.Users.Where(x => x.UserId == account.UserId && x.IsActive && x.Phone == account.Phone && x.PasswordHash == account.PasswordHash &&
                (account.CitizenIdHash == null
                    ? RoleCodes.StaffRoles.Contains(x.Role.RoleCode)
                    : x.Role.RoleCode == RoleCodes.Patient && db.Patients.Any(p => p.UserId == x.UserId && p.Phone == account.Phone && p.CitizenIdHash == account.CitizenIdHash)))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.PasswordHash, passwordHash)
                .SetProperty(x => x.TokenVersion, x => x.TokenVersion + 1)
                .SetProperty(x => x.UpdatedAt, clock.GetUtcNow().UtcDateTime), ct);
        if (changed != 1) return false;
        await audit.LogRequiredAsync("MOD_AUTH_PASSWORD_RECOVERED", "User", account.UserId, account.UserId,
            "{\"channel\":\"sms\",\"previousSessionsRevoked\":true}", ct: ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
