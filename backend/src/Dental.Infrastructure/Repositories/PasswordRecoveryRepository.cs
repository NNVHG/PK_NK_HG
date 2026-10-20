using System.Data;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class PasswordRecoveryRepository(DentalDbContext db, IAuditLogger audit, TimeProvider clock) : IPasswordRecoveryRepository
{
    public Task<RecoveryAccount?> FindActiveAsync(string phone, CancellationToken ct)
        => db.Users.AsNoTracking().Where(x => x.Phone == phone && x.IsActive)
            .Select(x => new RecoveryAccount(x.UserId, x.PasswordHash, x.Phone)).SingleOrDefaultAsync(ct);

    public async Task<bool> ResetAsync(RecoveryAccount account, string passwordHash, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Atomic compare-and-set also invalidates a code after a normal password change, role lock or competing reset.
        var changed = await db.Users.Where(x => x.UserId == account.UserId && x.IsActive && x.Phone == account.Phone && x.PasswordHash == account.PasswordHash)
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
