using Dental.Application.Features.MOD_PAT;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

internal static class IdentityMigrationSmoke
{
    private const string PreviousMigration = "20261027194838_AllowMultipleAssistantsPerDentist";
    private const string IdentityMigration = "20261028155115_AddPatientIdentityMetadata";

    private sealed class LegacyPatient
    {
        public int PatientId { get; set; }
        public int PatientNumber { get; set; }
        public string FullName { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? CitizenIdHash { get; set; }
        public string? CitizenIdMasked { get; set; }
        public int CitizenIdVersion { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // Ánh xạ schema cũ để ghi dữ liệu trước khi migration thêm metadata.
    private sealed class LegacyContext(DbContextOptions<LegacyContext> options) : DbContext(options)
    {
        public DbSet<LegacyPatient> Patients => Set<LegacyPatient>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<LegacyPatient>().ToTable("Patients").HasKey(patient => patient.PatientId);
            builder.Entity<LegacyPatient>().Property(patient => patient.PatientNumber)
                .HasDefaultValueSql("nextval('patient_number_seq')");
        }
    }

    public static async Task RunAsync(DentalDbContext db, DbContextOptions<DentalDbContext> options,
        Action<bool, string> check)
    {
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Owned test database required");

        var migrations = db.Database.GetMigrations().ToArray();
        check(Array.IndexOf(migrations, IdentityMigration) == Array.IndexOf(migrations, PreviousMigration) + 1,
            "Metadata migration follows the previous schema without rewriting history");
        await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        const string citizenId = "987654320123";
        var oldHash = CitizenIdIdentity.Hash(citizenId);
        int patientId;
        await using (var legacy = new LegacyContext(new DbContextOptionsBuilder<LegacyContext>()
            .UseNpgsql(db.Database.GetConnectionString()).Options))
        {
            var patient = new LegacyPatient
            {
                FullName = "Synthetic legacy identity", DateOfBirth = new(1990, 1, 1),
                Phone = "0990000777", CitizenIdHash = oldHash, CitizenIdMasked = "987******123"
            };
            legacy.Patients.Add(patient);
            await legacy.SaveChangesAsync();
            patientId = patient.PatientId;
        }

        await db.Database.MigrateAsync();
        var before = await db.Patients.AsNoTracking().SingleAsync(patient => patient.PatientId == patientId);
        check(before.CitizenIdHash == oldHash && before.CitizenIdMasked == "987******123" &&
            before.CitizenIdVersion == 0 && before.DocumentType is null && before.HashScheme is null &&
            before.IdentityKeyVersion is null && before.IssuingCountryCode is null,
            "Migration preserves legacy SHA-256 and leaves unverified metadata unset");
        var legacyInventory = await IdentityMigrationPreflight.InspectAsync(db);
        check(legacyInventory is { TotalPatients: 1, UnclassifiedHash: 1, CurrentHmac: 0,
            MissingHash: 0, MalformedHash: 0, DuplicateHashGroups: 0 },
            "Read-only inventory treats a legacy 64-character hash as unclassified, not verified HMAC");

        var role = new Role { RoleCode = RoleCodes.Receptionist, RoleName = "Synthetic receptionist" };
        var actor = new User
        {
            Role = role, FullName = "Synthetic identity clerk", Phone = "0990000778",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Synthetic123!"), IsActive = true
        };
        db.Users.Add(actor);
        await db.SaveChangesAsync();
        const string testKey = "SyntheticIdentityKeyForRecoverySmoke2026";
        var keyConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdentitySecurity:HmacKey"] = testKey
        }).Build();
        var hasher = new KeyedIdentityHasher(keyConfig);
        await using (var verified = new DentalDbContext(options))
        {
            var result = await new PatientCitizenIdRepository(verified, hasher).SaveAsync(
                patientId, actor.UserId, RoleCodes.Receptionist, new(citizenId, 0), null, default);
            check(result.IsSuccess && result.Value!.Version == 1,
                "Verified clerk replaces legacy identity with version guard");
        }

        var after = await db.Patients.AsNoTracking().SingleAsync(patient => patient.PatientId == patientId);
        check(after.CitizenIdHash == hasher.Hash(citizenId) && after.CitizenIdHash != oldHash &&
            after.CitizenIdMasked == "987******123" && after.CitizenIdVersion == 1 &&
            after.DocumentType == IdentityDocumentType.CitizenId && after.HashScheme == IdentityHashScheme.HmacCanonical &&
            after.IdentityKeyVersion == IdentityDocumentConstants.CurrentHmacKeyVersion && after.IssuingCountryCode is null,
            "Verified conversion stores only canonical HMAC, mask and key version");
        await using (var restored = new DentalDbContext(options))
        {
            // Tạo lại dịch vụ và DbContext như sau khi phục hồi cấu hình/CSDL.
            var restoredHasher = new KeyedIdentityHasher(keyConfig);
            check(await restored.Patients.AsNoTracking().AnyAsync(patient => patient.PatientId == patientId &&
                patient.CitizenIdHash == restoredHasher.Hash(citizenId) &&
                patient.IdentityKeyVersion == IdentityDocumentConstants.CurrentHmacKeyVersion),
                "Restored configuration key resolves the converted identity in a new database context");

            var changedKeyConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdentitySecurity:HmacKey"] = "DifferentSyntheticIdentityKeyForSmoke2026"
            }).Build();
            var changedKeyHasher = new KeyedIdentityHasher(changedKeyConfig);
            check(!changedKeyHasher.Matches(after.CitizenIdHash, citizenId),
                "An unbacked replacement key cannot resolve the converted identity");
        }
        check(await db.AuditLogs.AsNoTracking().CountAsync(log => log.EntityId == patientId &&
            log.Action == "MOD_PAT_CITIZEN_ID_UPDATED" && log.Detail != null &&
            !log.Detail.Contains(citizenId) && !log.Detail.Contains(oldHash) && !log.Detail.Contains(after.CitizenIdHash!)) == 1,
            "Conversion commits one audit entry without clear identifier or hash");
        db.Patients.AddRange(
            new Patient { FullName = "Synthetic missing identity", DateOfBirth = new(1990, 1, 1), Phone = "0990000781" },
            new Patient { FullName = "Synthetic malformed identity", DateOfBirth = new(1990, 1, 1), Phone = "0990000782",
                CitizenIdHash = "INVALID" },
            new Patient { FullName = "Synthetic incomplete passport", DateOfBirth = new(1990, 1, 1), Phone = "0990000783",
                CitizenIdHash = new string('A', 64), CitizenIdMasked = "AB****12",
                DocumentType = IdentityDocumentType.Passport, HashScheme = IdentityHashScheme.HmacCanonical,
                IdentityKeyVersion = IdentityDocumentConstants.CurrentHmacKeyVersion },
            new Patient { FullName = "Synthetic other key version", DateOfBirth = new(1990, 1, 1), Phone = "0990000784",
                CitizenIdHash = new string('B', 64), CitizenIdMasked = "987******124",
                DocumentType = IdentityDocumentType.CitizenId, HashScheme = IdentityHashScheme.HmacCanonical,
                IdentityKeyVersion = IdentityDocumentConstants.CurrentHmacKeyVersion + 1 });
        await db.SaveChangesAsync();
        var trackedBefore = db.ChangeTracker.Entries<Patient>().Count();
        var auditBefore = await db.AuditLogs.CountAsync();
        var inventory = await IdentityMigrationPreflight.InspectAsync(db);
        check(inventory is { TotalPatients: 5, CurrentHmac: 1, UnclassifiedHash: 0, MissingHash: 1,
            MalformedHash: 1, InvalidMetadata: 1, DuplicateHashGroups: 0, NonCurrentKeyVersion: 1 },
            "Inventory counts incomplete identities and other key versions without returning identifiers");
        check(db.ChangeTracker.Entries<Patient>().Count() == trackedBefore &&
            await db.AuditLogs.CountAsync() == auditBefore,
            "Inventory does not track patient entities or write audit records");
        check((await db.Database.GetPendingMigrationsAsync()).Any() == false,
            "Upgraded database has no pending migration");

        // --- Slice C3: IdentityMigrationRunner tests ---
        // 1. Guard production database
        var prodDbNameOptions = new DbContextOptionsBuilder<DentalDbContext>()
            .UseNpgsql("Host=localhost;Database=dental_db;Username=postgres;Password=postgres").Options;
        await using (var prodDb = new DentalDbContext(prodDbNameOptions))
        {
            var blocked = false;
            try
            {
                IdentityMigrationRunner.AssertNotProductionDatabase(prodDb);
            }
            catch (InvalidOperationException)
            {
                blocked = true;
            }
            check(blocked, "Production database 'dental_db' is guarded and rejected by IdentityMigrationRunner");
        }

        // 2. Safe inspect
        var safeInventory = await IdentityMigrationRunner.InspectSafeAsync(db);
        check(safeInventory.TotalPatients == 5, "InspectSafeAsync executes successfully on isolated test database");

        // 3. Batch conversion: Prepare a legacy patient to convert
        const string citizenIdToMigrate = "012345678901";
        var legacySha = CitizenIdIdentity.Hash(citizenIdToMigrate);
        var pToMigrate = new Patient
        {
            FullName = "Synthetic patient for migration runner",
            DateOfBirth = new(1995, 5, 5),
            Phone = "0990000785",
            CitizenIdHash = legacySha,
            CitizenIdMasked = "012******901",
            CitizenIdVersion = 0
        };
        db.Patients.Add(pToMigrate);
        await db.SaveChangesAsync();

        var batchItem = new IdentityMigrationRunner.VerifiedIdentityItem(
            pToMigrate.PatientId,
            citizenIdToMigrate,
            IdentityDocumentType.CitizenId,
            null);

        // Dry-run
        var dryRunResult = await IdentityMigrationRunner.ConvertVerifiedBatchAsync(
            db, hasher, actor.UserId, [batchItem], isDryRun: true);
        check(dryRunResult is { TotalProcessed: 1, ConvertedCount: 1, AlreadyUpToDateCount: 0, IsDryRun: true },
            "Dry-run predicts 1 conversion without mutating state");

        var pBeforeConvert = await db.Patients.AsNoTracking().SingleAsync(p => p.PatientId == pToMigrate.PatientId);
        check(pBeforeConvert.CitizenIdHash == legacySha && pBeforeConvert.CitizenIdVersion == 0,
            "Dry-run left database patient record unchanged");

        // Execute conversion
        var execResult = await IdentityMigrationRunner.ConvertVerifiedBatchAsync(
            db, hasher, actor.UserId, [batchItem], isDryRun: false);
        check(execResult is { TotalProcessed: 1, ConvertedCount: 1, AlreadyUpToDateCount: 0, IsDryRun: false },
            "Execution converted 1 record to HMAC canonical");

        var pAfterConvert = await db.Patients.AsNoTracking().SingleAsync(p => p.PatientId == pToMigrate.PatientId);
        check(pAfterConvert.CitizenIdHash == hasher.Hash(citizenIdToMigrate) &&
              pAfterConvert.CitizenIdVersion == 1 &&
              pAfterConvert.HashScheme == IdentityHashScheme.HmacCanonical &&
              pAfterConvert.IdentityKeyVersion == IdentityDocumentConstants.CurrentHmacKeyVersion,
            "Executed conversion upgraded patient to canonical HMAC and incremented version");

        // Idempotency: execute again with same batch
        var rerunResult = await IdentityMigrationRunner.ConvertVerifiedBatchAsync(
            db, hasher, actor.UserId, [batchItem], isDryRun: false);
        check(rerunResult is { TotalProcessed: 1, ConvertedCount: 0, AlreadyUpToDateCount: 1, IsDryRun: false },
            "Idempotency: Re-running conversion skips already upgraded record with 0 conversions");

        var pAfterRerun = await db.Patients.AsNoTracking().SingleAsync(p => p.PatientId == pToMigrate.PatientId);
        check(pAfterRerun.CitizenIdVersion == 1 && pAfterRerun.CitizenIdHash == pAfterConvert.CitizenIdHash,
            "Idempotency: Patient version and hash remained identical without extra mutation");

        // Audit check: Verify MOD_PAT_IDENTITY_MIGRATED exists and contains no plaintext or hash
        var migrationAudit = await db.AuditLogs.AsNoTracking()
            .SingleOrDefaultAsync(log => log.EntityId == pToMigrate.PatientId && log.Action == "MOD_PAT_IDENTITY_MIGRATED");
        check(migrationAudit != null &&
              !migrationAudit.Detail!.Contains(citizenIdToMigrate) &&
              !migrationAudit.Detail.Contains(legacySha) &&
              !migrationAudit.Detail.Contains(pAfterConvert.CitizenIdHash!),
            "Migration runner logs audit entry without exposing plaintext identifier or raw hash");

    }
}
