using System.Diagnostics;
using Dental.Application.Features.MOD_PAT;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Data.Seeders;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

internal static class IdentityDumpRestoreSmoke
{
    private static void CheckIsolatedDbName(string dbName)
    {
        if (string.IsNullOrWhiteSpace(dbName) ||
            !dbName.StartsWith("dental_test_drill_", StringComparison.Ordinal) ||
            string.Equals(dbName, "dental_db", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dbName, "dental_clinic", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Guard: Cấm chạy trên production hoặc primary database '{dbName}'.");
        }
    }

    private static string ResolvePgToolPath(string toolExeName)
    {
        var pgRoot = @"C:\Program Files\PostgreSQL";
        if (Directory.Exists(pgRoot))
        {
            var candidate = Directory.GetDirectories(pgRoot)
                .OrderByDescending(d => d)
                .Select(d => Path.Combine(d, "bin", toolExeName))
                .FirstOrDefault(File.Exists);
            if (candidate != null)
            {
                return candidate;
            }
        }
        return toolExeName;
    }

    private static async Task CreateIsolatedDatabaseAsync(NpgsqlConnectionStringBuilder baseBuilder, string dbName)
    {
        CheckIsolatedDbName(dbName);
        var adminBuilder = new NpgsqlConnectionStringBuilder(baseBuilder.ConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        await using var conn = new NpgsqlConnection(adminBuilder.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{dbName}\";";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task DropIsolatedDatabaseAsync(NpgsqlConnectionStringBuilder baseBuilder, string dbName)
    {
        CheckIsolatedDbName(dbName);
        NpgsqlConnection.ClearAllPools();
        var adminBuilder = new NpgsqlConnectionStringBuilder(baseBuilder.ConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        await using var conn = new NpgsqlConnection(adminBuilder.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE);";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task ExecutePgDumpAsync(NpgsqlConnectionStringBuilder baseBuilder, string dbName, string dumpFilePath)
    {
        CheckIsolatedDbName(dbName);
        var pgDumpPath = ResolvePgToolPath("pg_dump.exe");
        var startInfo = new ProcessStartInfo(pgDumpPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment["PGPASSWORD"] = baseBuilder.Password ?? "postgres";
        startInfo.ArgumentList.Add("-h");
        startInfo.ArgumentList.Add(baseBuilder.Host ?? "localhost");
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(baseBuilder.Port.ToString());
        startInfo.ArgumentList.Add("-U");
        startInfo.ArgumentList.Add(baseBuilder.Username ?? "postgres");
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(dbName);
        startInfo.ArgumentList.Add("-Fc");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(dumpFilePath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Không thể khởi động pg_dump process.");
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"pg_dump thất bại (exit {process.ExitCode}): {stderr}");
        }
    }

    private static async Task ExecutePgRestoreAsync(NpgsqlConnectionStringBuilder baseBuilder, string dbName, string dumpFilePath)
    {
        CheckIsolatedDbName(dbName);
        var pgRestorePath = ResolvePgToolPath("pg_restore.exe");
        var startInfo = new ProcessStartInfo(pgRestorePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment["PGPASSWORD"] = baseBuilder.Password ?? "postgres";
        startInfo.ArgumentList.Add("-h");
        startInfo.ArgumentList.Add(baseBuilder.Host ?? "localhost");
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(baseBuilder.Port.ToString());
        startInfo.ArgumentList.Add("-U");
        startInfo.ArgumentList.Add(baseBuilder.Username ?? "postgres");
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(dbName);
        startInfo.ArgumentList.Add("--no-owner");
        startInfo.ArgumentList.Add("--no-privileges");
        startInfo.ArgumentList.Add(dumpFilePath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Không thể khởi động pg_restore process.");
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"pg_restore thất bại (exit {process.ExitCode}): {stderr}");
        }
    }

    public static async Task RunAsync(string baseConnectionString, Action<bool, string> check)
    {
        var runId = Guid.NewGuid().ToString("N");
        var srcDbName = "dental_test_drill_src_" + runId;
        var dstDbName = "dental_test_drill_dst_" + runId;
        var dumpFilePath = Path.Combine(Path.GetTempPath(), $"dental_drill_{runId}.dump");

        CheckIsolatedDbName(srcDbName);
        CheckIsolatedDbName(dstDbName);

        var baseBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString);
        var srcBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = srcDbName };
        var dstBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = dstDbName };

        var srcOptions = new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql(srcBuilder.ConnectionString).Options;
        var dstOptions = new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql(dstBuilder.ConnectionString).Options;

        try
        {
            await CreateIsolatedDatabaseAsync(baseBuilder, srcDbName);
            check(true, $"Tạo CSDL nguồn cô lập thành công: {srcDbName}");

            await SeedSourceDatabaseAsync(srcOptions, check);
            await RunVerificationAsync(baseBuilder, srcOptions, dstOptions, srcDbName, dstDbName, dumpFilePath, check);
        }
        finally
        {
            if (File.Exists(dumpFilePath))
            {
                try { File.Delete(dumpFilePath); } catch { }
            }
            try { await DropIsolatedDatabaseAsync(baseBuilder, srcDbName); } catch { }
            try { await DropIsolatedDatabaseAsync(baseBuilder, dstDbName); } catch { }
        }
    }

    private static async Task SeedSourceDatabaseAsync(DbContextOptions<DentalDbContext> srcOptions, Action<bool, string> check)
    {
        await using var srcDb = new DentalDbContext(srcOptions);
        await srcDb.Database.MigrateAsync();
        check(!((await srcDb.Database.GetPendingMigrationsAsync()).Any()),
            "CSDL nguồn đã áp dụng đầy đủ toàn bộ migration hiện có");

        var seedConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seed:AdminPhone"] = "0999999001",
            ["Seed:AdminPassword"] = "SyntheticAdmin123!",
            ["Seed:DemoUsers"] = "true",
            ["Seed:DemoPassword"] = "SyntheticDemo123!",
            ["Seed:Phase1"] = "false",
        }).Build();

        using var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
        var seedLogger = loggerFactory.CreateLogger("IdentityDumpRestoreSmoke");
        await DatabaseSeeder.SeedAsync(srcDb, seedConfig, seedLogger, isDevelopment: true);
        await ClinicalDemoDataSeeder.SeedAsync(srcDb, seedLogger);
        check(await srcDb.Patients.AnyAsync() && await srcDb.Invoices.AnyAsync() && await srcDb.StockBalances.AnyAsync(),
            "CSDL nguồn nạp đầy đủ dữ liệu tổng hợp: hồ sơ bệnh nhân, hóa đơn, thanh toán, kho");

        const string keyV1 = "SyntheticKeyV1AtLeast32BytesLength123456";
        const string keyV2 = "SyntheticKeyV2AtLeast32BytesLength123456";
        var multiKeyConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdentitySecurity:Keys:1"] = keyV1,
            ["IdentitySecurity:Keys:2"] = keyV2,
            ["IdentitySecurity:CurrentVersion"] = "2"
        }).Build();
        var hasher = new KeyedIdentityHasher(multiKeyConfig);

        const string cccdV1 = "079090000001";
        const string cccdV2 = "079090000002";
        const string legacyCccd = "079090000003";
        const string passportNum = "B9876543";
        const string passportCountry = "VN";

        var hashV1 = hasher.Hash(cccdV1, IdentityDocumentType.CitizenId, keyVersion: 1);
        var hashV2 = hasher.Hash(cccdV2, IdentityDocumentType.CitizenId, keyVersion: 2);
        var legacyHash = CitizenIdIdentity.Hash(legacyCccd);
        var passportHash = hasher.Hash(passportNum, IdentityDocumentType.Passport, nationalityCode: passportCountry, keyVersion: 2);

        srcDb.Patients.AddRange(
            new Patient
            {
                FullName = "Bệnh nhân HMAC V1", DateOfBirth = new DateOnly(1985, 1, 1), Phone = "0981000001",
                CitizenIdHash = hashV1, CitizenIdMasked = hasher.Mask(cccdV1), CitizenIdVersion = 1,
                DocumentType = IdentityDocumentType.CitizenId, HashScheme = IdentityHashScheme.HmacCanonical,
                IdentityKeyVersion = 1, IsActive = true, CreatedAt = DateTime.UtcNow
            },
            new Patient
            {
                FullName = "Bệnh nhân HMAC V2", DateOfBirth = new DateOnly(1986, 2, 2), Phone = "0981000002",
                CitizenIdHash = hashV2, CitizenIdMasked = hasher.Mask(cccdV2), CitizenIdVersion = 1,
                DocumentType = IdentityDocumentType.CitizenId, HashScheme = IdentityHashScheme.HmacCanonical,
                IdentityKeyVersion = 2, IsActive = true, CreatedAt = DateTime.UtcNow
            },
            new Patient
            {
                FullName = "Bệnh nhân Legacy SHA256", DateOfBirth = new DateOnly(1987, 3, 3), Phone = "0981000003",
                CitizenIdHash = legacyHash, CitizenIdMasked = "079******003", CitizenIdVersion = 0,
                DocumentType = null, HashScheme = null, IdentityKeyVersion = null, IsActive = true, CreatedAt = DateTime.UtcNow
            },
            new Patient
            {
                FullName = "Bệnh nhân Hộ chiếu", DateOfBirth = new DateOnly(1988, 4, 4), Phone = "0981000004",
                CitizenIdHash = passportHash, CitizenIdMasked = hasher.Mask(passportNum, IdentityDocumentType.Passport),
                CitizenIdVersion = 1, DocumentType = IdentityDocumentType.Passport, HashScheme = IdentityHashScheme.HmacCanonical,
                IdentityKeyVersion = 2, IssuingCountryCode = passportCountry, IsActive = true, CreatedAt = DateTime.UtcNow
            }
        );

        var adminUser = await srcDb.Users.FirstAsync(u => u.Role.RoleCode == RoleCodes.Admin);
        srcDb.AuditLogs.Add(new AuditLog
        {
            UserId = adminUser.UserId, EntityType = "Patient", EntityId = 1,
            Action = "DRILL_PRE_DUMP_VERIFICATION", Detail = "{\"drill\":\"Slice_C4_Dump_Restore\"}",
            CreatedAt = DateTime.UtcNow
        });
        await srcDb.SaveChangesAsync();
    }

    private static async Task RunVerificationAsync(
        NpgsqlConnectionStringBuilder baseBuilder,
        DbContextOptions<DentalDbContext> srcOptions,
        DbContextOptions<DentalDbContext> dstOptions,
        string srcDbName,
        string dstDbName,
        string dumpFilePath,
        Action<bool, string> check)
    {
        int srcUserCount, srcPatientCount, srcVisitCount, srcInvoiceCount, srcPaymentCount, srcAuditCount, srcStockBalanceCount;
        decimal srcTotalPaid, srcTotalBalance, srcTotalStockOnHand;
        IdentityInventory srcInventory;
        await using (var srcDb = new DentalDbContext(srcOptions))
        {
            srcUserCount = await srcDb.Users.CountAsync();
            srcPatientCount = await srcDb.Patients.CountAsync();
            srcVisitCount = await srcDb.Visits.CountAsync();
            srcInvoiceCount = await srcDb.Invoices.CountAsync();
            srcPaymentCount = await srcDb.PaymentTransactions.CountAsync();
            srcAuditCount = await srcDb.AuditLogs.CountAsync();
            srcStockBalanceCount = await srcDb.StockBalances.CountAsync();
            srcTotalPaid = await srcDb.Invoices.SumAsync(i => i.PaidAmount);
            srcTotalBalance = await srcDb.Invoices.SumAsync(i => i.TotalAmount - i.DiscountAmount - i.PaidAmount);
            srcTotalStockOnHand = await srcDb.StockBalances.SumAsync(s => s.OnHand);
            srcInventory = await IdentityMigrationPreflight.InspectAsync(srcDb);
        }

        check(srcPatientCount >= 8, "CSDL nguồn có đủ hồ sơ bệnh nhân tổng hợp");
        check(srcTotalPaid > 0, $"Tổng doanh thu đã thu trên nguồn > 0 ({srcTotalPaid:#,##0} VND)");
        check(srcTotalStockOnHand > 0, $"Tổng tồn kho vật tư trên nguồn > 0 ({srcTotalStockOnHand} đơn vị)");

        await ExecutePgDumpAsync(baseBuilder, srcDbName, dumpFilePath);
        check(File.Exists(dumpFilePath) && new FileInfo(dumpFilePath).Length > 0,
            "pg_dump hoàn tất sinh tệp backup nhị phân nén (-Fc)");

        await CreateIsolatedDatabaseAsync(baseBuilder, dstDbName);
        check(true, $"Tạo CSDL đích cô lập thành công: {dstDbName}");

        await ExecutePgRestoreAsync(baseBuilder, dstDbName, dumpFilePath);
        check(true, "pg_restore phục hồi toàn bộ cấu trúc và dữ liệu sang CSDL đích thành công");

        await VerifyDestinationDatabaseAsync(dstOptions, srcUserCount, srcPatientCount, srcVisitCount,
            srcInvoiceCount, srcPaymentCount, srcAuditCount, srcStockBalanceCount,
            srcTotalPaid, srcTotalBalance, srcTotalStockOnHand, srcInventory, check);
    }

    private static async Task VerifyDestinationDatabaseAsync(
        DbContextOptions<DentalDbContext> dstOptions,
        int srcUserCount, int srcPatientCount, int srcVisitCount,
        int srcInvoiceCount, int srcPaymentCount, int srcAuditCount, int srcStockBalanceCount,
        decimal srcTotalPaid, decimal srcTotalBalance, decimal srcTotalStockOnHand,
        IdentityInventory srcInventory,
        Action<bool, string> check)
    {
        await using var dstDb = new DentalDbContext(dstOptions);

        var dstUserCount = await dstDb.Users.CountAsync();
        var dstPatientCount = await dstDb.Patients.CountAsync();
        var dstVisitCount = await dstDb.Visits.CountAsync();
        var dstInvoiceCount = await dstDb.Invoices.CountAsync();
        var dstPaymentCount = await dstDb.PaymentTransactions.CountAsync();
        var dstAuditCount = await dstDb.AuditLogs.CountAsync();
        var dstStockBalanceCount = await dstDb.StockBalances.CountAsync();

        check(dstUserCount == srcUserCount && dstPatientCount == srcPatientCount &&
              dstVisitCount == srcVisitCount && dstInvoiceCount == srcInvoiceCount &&
              dstPaymentCount == srcPaymentCount && dstAuditCount == srcAuditCount &&
              dstStockBalanceCount == srcStockBalanceCount,
            $"Dữ liệu số lượng bản ghi khớp 100% giữa Source và Destination (Users={dstUserCount}, Patients={dstPatientCount}, Visits={dstVisitCount}, Invoices={dstInvoiceCount}, Payments={dstPaymentCount}, Audits={dstAuditCount}, StockBalances={dstStockBalanceCount})");

        var dstTotalPaid = await dstDb.Invoices.SumAsync(i => i.PaidAmount);
        var dstTotalBalance = await dstDb.Invoices.SumAsync(i => i.TotalAmount - i.DiscountAmount - i.PaidAmount);
        var dstTotalStockOnHand = await dstDb.StockBalances.SumAsync(s => s.OnHand);
        var dstInventory = await IdentityMigrationPreflight.InspectAsync(dstDb);
        check(dstTotalPaid == srcTotalPaid && dstTotalBalance == srcTotalBalance,
            $"Tổng số tiền đã thu ({dstTotalPaid:#,##0} VND) và dư nợ ({dstTotalBalance:#,##0} VND) trên hóa đơn khớp chính xác");
        check(dstTotalStockOnHand == srcTotalStockOnHand,
            $"Tổng số lượng tồn kho vật tư khớp chính xác ({dstTotalStockOnHand} đơn vị)");
        check(dstInventory == srcInventory,
            "Kiểm kê phân loại định danh (IdentityMigrationPreflight) sau phục hồi khớp 100% với bản gốc");

        var orphanInvoices = await dstDb.Invoices.Where(i => !dstDb.Visits.Any(v => v.VisitId == i.VisitId)).CountAsync();
        var orphanPayments = await dstDb.PaymentTransactions.Where(p => !dstDb.Invoices.Any(i => i.Id == p.InvoiceId)).CountAsync();
        var orphanVisits = await dstDb.Visits.Where(v => !dstDb.Patients.Any(p => p.PatientId == v.PatientId)).CountAsync();
        var orphanStockIssues = await dstDb.StockIssues.Where(s => !dstDb.Visits.Any(v => v.VisitId == s.VisitId)).CountAsync();

        check(orphanInvoices == 0 && orphanPayments == 0 && orphanVisits == 0 && orphanStockIssues == 0,
            "Tính toàn vẹn khóa ngoại (FK constraints) được bảo toàn toàn vẹn sau khi phục hồi");

        await VerifyIdentityLookupsAndKeyGuardsAsync(dstDb, check);
    }

    private static async Task VerifyIdentityLookupsAndKeyGuardsAsync(DentalDbContext dstDb, Action<bool, string> check)
    {
        const string keyV1 = "SyntheticKeyV1AtLeast32BytesLength123456";
        const string keyV2 = "SyntheticKeyV2AtLeast32BytesLength123456";
        var validConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdentitySecurity:Keys:1"] = keyV1,
            ["IdentitySecurity:Keys:2"] = keyV2,
            ["IdentitySecurity:CurrentVersion"] = "2"
        }).Build();
        var restoredHasher = new KeyedIdentityHasher(validConfig);

        const string cccdV1 = "079090000001";
        const string cccdV2 = "079090000002";
        const string legacyCccd = "079090000003";
        const string passportNum = "B9876543";
        const string passportCountry = "VN";

        var pV1 = await dstDb.Patients.FirstOrDefaultAsync(p => p.Phone == "0981000001");
        var pV2 = await dstDb.Patients.FirstOrDefaultAsync(p => p.Phone == "0981000002");
        var pLegacy = await dstDb.Patients.FirstOrDefaultAsync(p => p.Phone == "0981000003");
        var pPassport = await dstDb.Patients.FirstOrDefaultAsync(p => p.Phone == "0981000004");

        check(pV1 != null && restoredHasher.Matches(pV1.CitizenIdHash, cccdV1, IdentityDocumentType.CitizenId),
            "Phục hồi giải mã và đối chiếu thành công hồ sơ băm bằng HMAC khóa V1");
        check(pV2 != null && restoredHasher.Matches(pV2.CitizenIdHash, cccdV2, IdentityDocumentType.CitizenId),
            "Phục hồi giải mã và đối chiếu thành công hồ sơ băm bằng HMAC khóa V2");
        check(pLegacy != null && restoredHasher.Matches(pLegacy.CitizenIdHash, legacyCccd, IdentityDocumentType.CitizenId),
            "Phục hồi đối chiếu thành công hồ sơ băm bằng Legacy SHA-256");
        check(pPassport != null && restoredHasher.Matches(pPassport.CitizenIdHash, passportNum, IdentityDocumentType.Passport, passportCountry),
            "Phục hồi giải mã và đối chiếu thành công hồ sơ Hộ chiếu quốc tế");

        var wrongKeyConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IdentitySecurity:Keys:1"] = "WrongKeyThatCannotMatchV1Identity202610",
            ["IdentitySecurity:Keys:2"] = "WrongKeyThatCannotMatchV2Identity202610",
            ["IdentitySecurity:CurrentVersion"] = "2"
        }).Build();
        var wrongHasher = new KeyedIdentityHasher(wrongKeyConfig);

        check(!wrongHasher.Matches(pV1!.CitizenIdHash, cccdV1, IdentityDocumentType.CitizenId),
            "Khóa cấu hình sai bị từ chối đối chiếu hồ sơ V1 (Fail-closed)");
        check(!wrongHasher.Matches(pV2!.CitizenIdHash, cccdV2, IdentityDocumentType.CitizenId),
            "Khóa cấu hình sai bị từ chối đối chiếu hồ sơ V2 (Fail-closed)");
        check(!wrongHasher.Matches(pPassport!.CitizenIdHash, passportNum, IdentityDocumentType.Passport, passportCountry),
            "Khóa cấu hình sai bị từ chối đối chiếu hồ sơ Hộ chiếu (Fail-closed)");

        var missingKeyConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var threwOnMissingKey = false;
        try
        {
            _ = new KeyedIdentityHasher(missingKeyConfig);
        }
        catch (InvalidOperationException)
        {
            threwOnMissingKey = true;
        }
        check(threwOnMissingKey, "Khởi động hệ thống khi thiếu khóa HMAC cấu hình bị từ chối an toàn (Dừng an toàn)");
    }
}
