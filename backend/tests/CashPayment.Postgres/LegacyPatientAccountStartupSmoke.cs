using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static class LegacyPatientAccountStartupSmoke
{
    private const string PreConversionMigration = "20261023145000_RenameLegacyAccountConversionMarker";
    private const string UniqueOwnershipMigration = "20261023150000_EnforceIndividualPatientOwnership";
    private const string ConversionAudit = "MOD_AUTH_LEGACY_PATIENT_CONVERSION";

    public static async Task RunAsync(string connectionString, Action<bool, string> check)
    {
        var ownedName = "dental_cash_test_" + Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnectionStringBuilder(connectionString) { Database = ownedName };
        OwnedSmokeApi.ValidateDatabase(connection.ConnectionString, ownedName);
        var options = new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql(connection.ConnectionString).Options;
        await using var db = new DentalDbContext(options);
        try
        {
            await db.Database.MigrateAsync(PreConversionMigration);
            await db.Database.ExecuteSqlRawAsync("""
                WITH role AS (
                    INSERT INTO "Roles" ("RoleCode", "RoleName", "CreatedAt")
                    VALUES ('PATIENT', 'Synthetic legacy role', now())
                    RETURNING "RoleId"
                ), account AS (
                    INSERT INTO "Users" ("RoleId", "Phone", "PasswordHash", "FullName", "IsActive",
                        "RequiresIndividualIdentityConversion", "TokenVersion", "CreatedAt")
                    SELECT "RoleId", '0999999101', 'synthetic legacy hash', 'Synthetic account', true, true, 4, now()
                    FROM role
                    RETURNING "UserId"
                )
                INSERT INTO "Patients" ("UserId", "FullName", "DateOfBirth", "Phone",
                    "CitizenIdHash", "CitizenIdVersion", "CreatedAt")
                SELECT "UserId", data.name, date '1990-01-01', '0999999102', data.hash, 0, now()
                FROM account CROSS JOIN (VALUES ('Synthetic first', repeat('a', 64)), ('Synthetic second', NULL)) data(name, hash);
                """);

            await using (var rejected = new OwnedSmokeApi(options, ownedName))
            {
                var stopped = false;
                try { using var client = await rejected.StartAsync(); }
                catch (InvalidOperationException error) when (error.Message.Contains(nameof(LegacyPatientAccountConversion)))
                {
                    stopped = true;
                }
                check(stopped, "API startup rejects a legacy profile with missing identity");
            }
            var originalUserId = await db.Users.Select(user => user.UserId).SingleAsync();
            check(await db.Patients.CountAsync(patient => patient.UserId == originalUserId) == 2 &&
                  !await db.AuditLogs.AnyAsync() &&
                  !(await db.Database.GetAppliedMigrationsAsync()).Contains(UniqueOwnershipMigration),
                "Rejected conversion preserves both original links and leaves the unique migration pending");

            await db.Patients.Where(patient => patient.CitizenIdHash == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(patient => patient.CitizenIdHash, new string('B', 64)));
            await using (var upgraded = new OwnedSmokeApi(options, ownedName))
            {
                using var client = await upgraded.StartAsync();
                check(!(await db.Database.GetPendingMigrationsAsync()).Any(),
                    "API startup converts legacy data before applying all remaining migrations");
            }

            var linkedPatients = await db.Patients.AsNoTracking().Include(patient => patient.User).ToArrayAsync();
            check(linkedPatients.Length == 2 &&
                  linkedPatients.Select(patient => patient.UserId).Distinct().Count() == 2 &&
                  linkedPatients.All(patient => patient.User is { LoginByCitizenId: true, RequiresPhoneActivation: true } user &&
                      user.UserId != originalUserId && BCrypt.Net.BCrypt.Verify(patient.Phone, user.PasswordHash)),
                "Both profiles receive separate accounts with their initial activation credentials");
            check(linkedPatients.Any(patient => patient.CitizenIdHash == new string('A', 64) && patient.CitizenIdVersion == 1),
                "Lowercase identity hash is normalized using its original concurrency version");
            check(await db.Users.AnyAsync(user => user.UserId == originalUserId && !user.IsActive && user.TokenVersion == 5) &&
                  await db.AuditLogs.CountAsync(log => log.Action == ConversionAudit) == 2,
                "Conversion revokes the source account and audits each reassigned profile");

            await using var restarted = new OwnedSmokeApi(options, ownedName);
            using var restartedClient = await restarted.StartAsync();
            check(await db.Users.CountAsync() == 4 && await db.AuditLogs.CountAsync(log => log.Action == ConversionAudit) == 2,
                "Restart neither duplicates converted accounts nor replays conversion audit entries");
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
