using System.Data;
using Dental.Application.Common;
using Dental.Domain.Enums;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class PasswordRecoveryRepository(DentalDbContext db, IAuditLogger audit, TimeProvider clock, IKeyedIdentityHasher identityHasher) : IPasswordRecoveryRepository
{
    private readonly VietnamClock _vietnamClock = new(clock);

    public async Task<RecoveryAccount?> FindActiveAsync(string phone, string? citizenId, CancellationToken ct, IdentityDocumentType documentType = IdentityDocumentType.CitizenId, string? nationalityCode = null)
    {
        if (!string.IsNullOrEmpty(citizenId))
        {
            var cutoff = PatientAccountAge.LatestEligibleBirthDate(_vietnamClock.Today);
            var hash = identityHasher.Hash(citizenId, documentType, nationalityCode);
            var country = nationalityCode?.Trim().ToUpperInvariant();
            var legacyHash = documentType == IdentityDocumentType.CitizenId
                ? Dental.Application.Features.MOD_PAT.CitizenIdIdentity.Hash(citizenId.Trim()) : null;
            var matches = await db.Patients.AsNoTracking()
                .Where(p => (p.CitizenIdHash == hash || legacyHash != null && p.CitizenIdHash == legacyHash) &&
                            (documentType == IdentityDocumentType.Passport
                                ? p.DocumentType == IdentityDocumentType.Passport && p.IssuingCountryCode == country
                                : p.DocumentType == null || p.DocumentType == IdentityDocumentType.CitizenId) &&
                            p.IsActive && p.DateOfBirth <= cutoff &&
                            p.User != null && p.User.Phone == phone && p.User.IsActive &&
                            p.User.Role.RoleCode == RoleCodes.Patient)
                .Select(p => new RecoveryAccount(p.User!.UserId, p.User.PasswordHash, p.User.Phone, p.CitizenIdHash, p.User.TokenVersion, p.DocumentType, p.IssuingCountryCode))
                .Take(2).ToListAsync(ct);
            return matches.Count == 1 ? matches[0] : null;
        }

        var staff = await db.Users.AsNoTracking()
            .Where(u => u.Phone == phone && u.IsActive && RoleCodes.StaffRoles.Contains(u.Role.RoleCode) &&
                (!u.RequiresPhoneActivation || u.PhoneActivationReason != PhoneActivationReason.PhoneChange))
            .Select(u => new RecoveryAccount(u.UserId, u.PasswordHash, u.Phone, null, u.TokenVersion, null, null))
            .Take(2).ToListAsync(ct);
        return staff.Count == 1 ? staff[0] : null;
    }

    public async Task<bool> ResetAsync(RecoveryAccount account, string passwordHash, CancellationToken ct)
    {
        var cutoff = PatientAccountAge.LatestEligibleBirthDate(_vietnamClock.Today);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Atomic compare-and-set also invalidates a code after a normal password change, role lock or competing reset.
        var changed = await db.Users.Where(x => x.UserId == account.UserId && x.IsActive && x.Phone == account.Phone && x.PasswordHash == account.PasswordHash && x.TokenVersion == account.TokenVersion &&
                (account.CitizenIdHash == null
                    ? RoleCodes.StaffRoles.Contains(x.Role.RoleCode) &&
                        (!x.RequiresPhoneActivation || x.PhoneActivationReason != PhoneActivationReason.PhoneChange)
                    : x.Role.RoleCode == RoleCodes.Patient && db.Patients.Any(p => p.UserId == x.UserId && p.IsActive &&
                        p.DateOfBirth <= cutoff && p.CitizenIdHash == account.CitizenIdHash &&
                        p.DocumentType == account.DocumentType && p.IssuingCountryCode == account.IssuingCountryCode)))
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
