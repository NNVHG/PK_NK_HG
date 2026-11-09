using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
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
        // Startup runs this against the schema immediately before the unique Patient.UserId index.
        // Keep its mappings independent of columns added to the current model by later migrations.
        var options = new DbContextOptionsBuilder<ConversionDbContext>()
            .UseNpgsql(db.Database.GetDbConnection()).Options;
        await using var conversionDb = new ConversionDbContext(options);
        await using var transaction = await conversionDb.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var sourceUsers = await conversionDb.Users
            .Include(user => user.Role)
            .Where(user =>
                (user.RequiresIndividualIdentityConversion &&
                    (user.IsActive || conversionDb.Patients.Any(patient => patient.UserId == user.UserId))) ||
                conversionDb.Patients.Count(patient => patient.UserId == user.UserId) > 1)
            .OrderBy(user => user.UserId)
            .ToListAsync(cancellationToken);

        if (sourceUsers.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var sourceUserIds = sourceUsers.Select(user => user.UserId).ToArray();
        var patients = await conversionDb.Patients
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

        var allPatientHashes = await conversionDb.Patients
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
        var newUsersByPatient = new Dictionary<int, ConversionUser>(patients.Count);

        foreach (var patient in patients)
        {
            var phone = patient.Phone.Trim();
            var user = new ConversionUser
            {
                Phone = phone,
                LoginByCitizenId = true,
                RequiresIndividualIdentityConversion = false,
                PasswordHash = passwordHasher.Hash(phone),
                RequiresPhoneActivation = true,
                FullName = patient.FullName,
                Email = patient.Email,
                DateOfBirth = patient.DateOfBirth,
                Gender = patient.Gender,
                RoleId = patientRoleId,
                IsActive = patient.IsActive,
                CreatedAt = createdAt
            };

            newUsersByPatient.Add(patient.PatientId, user);
            conversionDb.Users.Add(user);
        }

        // Save once inside the transaction to obtain generated UserIds for the Patient links and audit trail.
        await conversionDb.SaveChangesAsync(cancellationToken);

        foreach (var patient in patients)
        {
            var oldUserId = patient.UserId!.Value;
            var newUser = newUsersByPatient[patient.PatientId];
            var normalizedHash = patient.CitizenIdHash!.ToUpperInvariant();
            if (patient.CitizenIdHash != normalizedHash)
            {
                patient.CitizenIdHash = normalizedHash;
                patient.CitizenIdVersion++;
            }
            patient.Phone = newUser.Phone;
            patient.UserId = newUser.UserId;
            patient.UpdatedAt = createdAt;
            conversionDb.AuditLogs.Add(new ConversionAuditLog
            {
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
            sourceUser.IsActive = false;
            sourceUser.RequiresIndividualIdentityConversion = true;
            sourceUser.TokenVersion++;
            sourceUser.UpdatedAt = createdAt;
            conversionDb.AuditLogs.Add(new ConversionAuditLog
            {
                Action = "MOD_AUTH_LEGACY_LOGIN_DISABLED",
                EntityType = "User",
                EntityId = sourceUser.UserId,
                Detail = JsonSerializer.Serialize(new { migration = "DL-347", sessionsRevoked = true }),
                CreatedAt = createdAt
            });
        }

        await conversionDb.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Bulk legacy Patient account conversion completed. ConvertedProfiles={ConvertedProfiles}, DisabledLegacyAccounts={DisabledLegacyAccounts}.",
            patients.Count, sourceUsers.Count);
        return patients.Count;
    }

    private sealed class ConversionDbContext(DbContextOptions<ConversionDbContext> options) : DbContext(options)
    {
        public DbSet<ConversionUser> Users => Set<ConversionUser>();
        public DbSet<ConversionPatient> Patients => Set<ConversionPatient>();
        public DbSet<ConversionAuditLog> AuditLogs => Set<ConversionAuditLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            var users = builder.Entity<ConversionUser>().ToTable("Users");
            users.HasKey(user => user.UserId);
            users.HasOne(user => user.Role).WithMany().HasForeignKey(user => user.RoleId);
            users.Property(user => user.TokenVersion).IsConcurrencyToken();
            users.Property(user => user.RequiresPhoneActivation).IsConcurrencyToken();
            builder.Entity<ConversionRole>().ToTable("Roles").HasKey(role => role.RoleId);

            // Patient.UserId can still repeat here. Map it as a scalar so tracking keeps every link.
            var patients = builder.Entity<ConversionPatient>().ToTable("Patients");
            patients.HasKey(patient => patient.PatientId);
            patients.Property(patient => patient.CitizenIdVersion).IsConcurrencyToken();

            builder.Entity<ConversionAuditLog>().ToTable("AuditLogs").HasKey(log => log.LogId);
        }
    }

    private sealed class ConversionUser
    {
        public int UserId { get; set; }
        public string Phone { get; set; } = string.Empty;
        public bool LoginByCitizenId { get; set; }
        public bool RequiresIndividualIdentityConversion { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public int TokenVersion { get; set; }
        public bool RequiresPhoneActivation { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public int RoleId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public ConversionRole Role { get; set; } = null!;
    }

    private sealed class ConversionRole
    {
        public int RoleId { get; set; }
        public string RoleCode { get; set; } = string.Empty;
    }

    private sealed class ConversionPatient
    {
        public int PatientId { get; set; }
        public int? UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateOnly DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? CitizenIdHash { get; set; }
        public int CitizenIdVersion { get; set; }
        public bool IsActive { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    private sealed class ConversionAuditLog
    {
        public long LogId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string? Detail { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
