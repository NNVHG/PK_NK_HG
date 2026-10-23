using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Data;

/// <summary>
/// One-time, all-or-nothing conversion of legacy accounts with multiple Patient links to individual Patient accounts.
/// Kept separate from EF schema migrations because BCrypt must be computed from the stored Patient phone.
/// </summary>
public sealed class LegacyPatientAccountConversion(
    DentalDbContext db,
    IPasswordHasher passwordHasher,
    ILogger<LegacyPatientAccountConversion> logger)
{
    private static readonly Regex CitizenIdHashPattern = new("^[A-Fa-f0-9]{64}$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new("^0[0-9]{9}$", RegexOptions.Compiled);

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        // Read detached: the new 1:1 model must not fix up old multiple links before conversion.
        var sourceUsers = await db.Users.AsNoTracking()
            .Include(user => user.Role)
            .Where(user =>
                (user.RequiresIndividualIdentityConversion &&
                    (user.IsActive || db.Patients.Any(patient => patient.UserId == user.UserId))) ||
                db.Patients.Count(patient => patient.UserId == user.UserId) > 1)
            .OrderBy(user => user.UserId)
            .ToListAsync(cancellationToken);

        if (sourceUsers.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var sourceUserIds = sourceUsers.Select(user => user.UserId).ToArray();
        var patients = await db.Patients.AsNoTracking()
            .Where(patient => patient.UserId.HasValue && sourceUserIds.Contains(patient.UserId.Value))
            .OrderBy(patient => patient.PatientId)
            .ToListAsync(cancellationToken);

        var invalidRoleCount = sourceUsers.Count(user => user.Role.RoleCode != RoleCodes.Patient);
        var invalidIdentityCount = patients.Count(patient =>
            patient.CitizenIdHash is null || !CitizenIdHashPattern.IsMatch(patient.CitizenIdHash) ||
            !PhonePattern.IsMatch(patient.Phone.Trim()));

        var targetHashGroups = patients
            .Where(patient => patient.CitizenIdHash is not null && CitizenIdHashPattern.IsMatch(patient.CitizenIdHash))
            .GroupBy(patient => patient.CitizenIdHash!.ToUpperInvariant(), StringComparer.Ordinal)
            .Count(group => group.Count() > 1);

        var allPatientHashes = await db.Patients
            .Where(patient => patient.CitizenIdHash != null)
            .Select(patient => patient.CitizenIdHash!)
            .ToListAsync(cancellationToken);
        var duplicateHashCount = allPatientHashes
            .GroupBy(hash => hash.ToUpperInvariant(), StringComparer.Ordinal)
            .Count(group => group.Count() > 1);

        if (invalidRoleCount > 0 || invalidIdentityCount > 0 || targetHashGroups > 0 || duplicateHashCount > 0)
        {
            logger.LogError(
                "Bulk legacy Patient account conversion aborted before writes. InvalidRoles={InvalidRoles}, InvalidProfiles={InvalidProfiles}, DuplicateTargetHashes={DuplicateTargetHashes}, DuplicateDatabaseHashes={DuplicateDatabaseHashes}. No account data was changed.",
                invalidRoleCount, invalidIdentityCount, targetHashGroups, duplicateHashCount);
            throw new InvalidOperationException(
                "Legacy Patient account conversion stopped: one or more linked profiles have missing or ambiguous identity data. No conversion was committed.");
        }

        var patientRoleId = sourceUsers.Select(user => user.RoleId).Distinct().Single();
        var createdAt = DateTime.UtcNow;
        var newUsersByPatient = new Dictionary<int, User>(patients.Count);

        foreach (var patient in patients)
        {
            var phone = patient.Phone.Trim();
            var user = new User
            {
                Phone = phone,
                LoginByCitizenId = true,
                RequiresIndividualIdentityConversion = false,
                PasswordHash = passwordHasher.Hash(phone),
                RequiresPhoneActivation = true,
                PhoneVerifiedAtUtc = null,
                FullName = patient.FullName,
                Email = patient.Email,
                DateOfBirth = patient.DateOfBirth,
                Gender = patient.Gender,
                RoleId = patientRoleId,
                IsActive = patient.IsActive,
                CreatedAt = createdAt
            };

            if (!string.Equals(patient.CitizenIdHash, patient.CitizenIdHash!.ToUpperInvariant(), StringComparison.Ordinal))
            {
                patient.CitizenIdHash = patient.CitizenIdHash.ToUpperInvariant();
                patient.CitizenIdVersion++;
                patient.UpdatedAt = createdAt;
            }

            patient.Phone = phone;
            newUsersByPatient.Add(patient.PatientId, user);
            db.Users.Add(user);
        }

        // Save once inside the transaction to obtain generated UserIds for the Patient links and audit trail.
        await db.SaveChangesAsync(cancellationToken);

        foreach (var patient in patients)
        {
            var oldUserId = patient.UserId!.Value;
            var newUser = newUsersByPatient[patient.PatientId];
            patient.UserId = newUser.UserId;
            patient.UpdatedAt = createdAt;
            db.Patients.Update(patient);
            db.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = "MOD_AUTH_LEGACY_PATIENT_CONVERSION",
                EntityType = "Patient",
                EntityId = patient.PatientId,
                Detail = JsonSerializer.Serialize(new
                {
                    sourceUserId = oldUserId,
                    targetUserId = newUser.UserId,
                    migration = "DL-347"
                }),
                CreatedAt = createdAt
            });
        }

        foreach (var sourceUser in sourceUsers)
        {
            // Attach the scalar account only; detached Role instances may share the same key.
            db.Entry(sourceUser).State = EntityState.Unchanged;
            sourceUser.IsActive = false;
            sourceUser.RequiresIndividualIdentityConversion = true;
            sourceUser.TokenVersion++;
            sourceUser.UpdatedAt = createdAt;
            db.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = "MOD_AUTH_LEGACY_LOGIN_DISABLED",
                EntityType = "User",
                EntityId = sourceUser.UserId,
                Detail = JsonSerializer.Serialize(new { migration = "DL-347", sessionsRevoked = true }),
                CreatedAt = createdAt
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Bulk legacy Patient account conversion completed. ConvertedProfiles={ConvertedProfiles}, DisabledLegacyAccounts={DisabledLegacyAccounts}.",
            patients.Count, sourceUsers.Count);
        return patients.Count;
    }
}
