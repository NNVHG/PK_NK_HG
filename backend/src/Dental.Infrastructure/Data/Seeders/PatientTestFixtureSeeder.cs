using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dental.Application.Features.MOD_IMG;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Data.Seeders;

/// <summary>
/// Development-only supplemental data from docs/TT_Benh_nhan_test.
/// It is deliberately opt-in and never loads patient fixtures in Production.
/// </summary>
public static class PatientTestFixtureSeeder
{
    private const string MarkerType = "TestFixturePack";
    private const string MarkerAction = "SEED_APPLIED";
    private const string MarkerDetail = "{\"fixture\":\"TT_Du_lieu_test_52_chuc_nang\",\"version\":1}";
    private const string SourceMarkerType = "TestPatientSource";
    private const string SourceMarkerDetail = "{\"fixture\":\"TT_Benh_nhan_test_demo\",\"version\":1}";

    private sealed record Person(
        string Key, string Name, DateOnly DateOfBirth, string Gender, string Phone,
        string? CitizenId, bool HasAccount, bool AccountActive = true, bool RequiresActivation = false);

    private static readonly Person[] People =
    [
        new("KH-101", "NGUYỄN MINH ANH (DEMO)", new(1992, 6, 15), "Nam", "0900000101", "079092001101", true),
        new("KH-102", "TRẦN VĂN HÙNG (DEMO)", new(1988, 8, 7), "Nam", "0900000102", "079088001102", true),
        new("KH-103", "TRẦN GIA HÂN (DEMO)", new(2019, 6, 3), "Nữ", "0900000102", null, false),
        new("KH-104", "LÊ BẢO NAM (DEMO)", new(2022, 3, 14), "Nam", "0900000104", null, false),
        new("KH-105", "PHẠM THU HÀ (DEMO)", new(1972, 8, 11), "Nữ", "0900000105", "079172001105", true, RequiresActivation: true),
        new("KH-106", "VÕ MINH TUẤN (DEMO)", new(1998, 10, 16), "Nam", "0900000106", "079098001106", true, AccountActive: false),
        new("KH-107", "LÊ NGỌC DIỆP (DEMO)", new(1996, 10, 21), "Nữ", "0900000101", "079196001107", true),
    ];

    public static async Task SeedAsync(
        DentalDbContext db,
        IConfiguration config,
        IKeyedIdentityHasher identityHasher,
        IDentalImageStorage imageStorage,
        string contentRoot,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (!config.GetValue<bool>("Seed:PatientTestFixtures")) return;

        await SeedSourcePatientProfilesAsync(db, identityHasher, contentRoot, logger, ct);

        var password = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Patient test fixtures: missing Seed:DemoPassword; skipped.");
            return;
        }

        if (await HasSeedMarkerAsync(db, ct)) return;

        var receptionist = await db.Users.FirstOrDefaultAsync(x => x.Phone == "0900000002", ct);
        var dentist1 = await db.Users.FirstOrDefaultAsync(x => x.Phone == "0900000003", ct);
        var assistant = await db.Users.FirstOrDefaultAsync(x => x.Phone == "0900000004", ct);
        var dentist2 = await db.Users.FirstOrDefaultAsync(x => x.Phone == "0900000013", ct);
        if (receptionist is null || dentist1 is null || assistant is null || dentist2 is null)
        {
            logger.LogWarning("Patient test fixtures: missing base demo staff accounts; skipped.");
            return;
        }

        var admin = await db.Users.FirstOrDefaultAsync(x => x.Role.RoleCode == RoleCodes.Admin, ct);
        var patientRole = await db.Roles.FirstOrDefaultAsync(x => x.RoleCode == RoleCodes.Patient, ct);
        if (admin is null || patientRole is null)
        {
            logger.LogWarning("Patient test fixtures: missing Admin or Patient role; skipped.");
            return;
        }

        var storagePaths = new List<string>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (await HasSeedMarkerAsync(db, ct))
            {
                await transaction.CommitAsync(ct);
                return;
            }

            var now = DateTime.UtcNow;
            var localNow = VietnamTime(now);
            var today = DateOnly.FromDateTime(localNow);
            var fixtureDate = today.AddDays(1);
            var patients = await SeedPeopleAsync(db, identityHasher, patientRole.RoleId, password, today, now, ct);

            var p101 = patients["KH-101"];
            var p102 = patients["KH-102"];
            var p103 = patients["KH-103"];
            var p105 = patients["KH-105"];
            var p106 = patients["KH-106"];
            var p107 = patients["KH-107"];

            if (!await db.PatientGuardians.AnyAsync(x => x.GuardianPatientId == p102.PatientId && x.WardPatientId == p103.PatientId && x.IsActive, ct))
            {
                db.PatientGuardians.Add(new PatientGuardian
                {
                    GuardianPatientId = p102.PatientId,
                    WardPatientId = p103.PatientId,
                    Relationship = "Cha",
                    IsActive = true,
                    AssignedAt = now,
                    AssignedByUserId = receptionist.UserId,
                    Notes = "Đã xác minh quan hệ trên dữ liệu giả lập tại quầy.",
                    CreatedAt = now,
                });
            }

            await AddOnlineBookingIfMissingAsync(db, "REQ-ONLINE-01", "ĐỖ THẢO LINH (DEMO)", "0900000108", new(1991, 9, 2), "Nữ", today.AddDays(1), new(10, 0), ct);
            await AddOnlineBookingIfMissingAsync(db, "REQ-ONLINE-02", People[2].Name, People[2].Phone, People[2].DateOfBirth, People[2].Gender, today.AddDays(2), new(15, 0), ct);
            await db.SaveChangesAsync(ct);

            var rooms = await db.ClinicRooms.OrderBy(x => x.RoomCode).ToListAsync(ct);
            var room1 = rooms.FirstOrDefault(x => x.RoomCode == "G01");
            var room2 = rooms.FirstOrDefault(x => x.RoomCode == "G02");

            var dueDate = localNow.TimeOfDay >= TimeSpan.FromHours(8) ? today : today.AddDays(-1);
            var dueIsToday = dueDate == today;
            var dueSlot = dueIsToday ? DueSlot(localNow.TimeOfDay) : new TimeOnly(17, 0);
            var counterVisit = NewVisit(p101, receptionist, dueIsToday ? null : dentist1, dueIsToday ? VisitStatuses.Created : VisitStatuses.Completed,
                dueIsToday ? null : UtcAt(dueDate, new(8, 0)), dueIsToday ? null : UtcAt(dueDate, new(8, 30)),
                dueIsToday ? null : room1?.Id, !dueIsToday, dueIsToday ? now : UtcAt(dueDate, new(8, 0)));

            var v101 = NewVisit(p101, dentist1, dentist1, VisitStatuses.Completed,
                UtcAt(fixtureDate, new(8, 0)), UtcAt(fixtureDate, new(9, 0)), room1?.Id, true, UtcAt(fixtureDate, new(8, 0)), "NK002", "Dữ liệu lâm sàng giả lập để kiểm tra sơ đồ răng.");
            var v101Open = NewVisit(p101, receptionist, null, VisitStatuses.Created, null, null, null, false, now.AddMinutes(-5));
            var v103 = NewVisit(p103, receptionist, null, VisitStatuses.Created, null, null, null, false, now.AddMinutes(-3));
            var v103Complete = NewVisit(p103, dentist1, dentist1, VisitStatuses.Completed,
                UtcAt(fixtureDate, new(11, 0)), UtcAt(fixtureDate, new(11, 20)), room1?.Id, true, UtcAt(fixtureDate, new(11, 0)), "NK001", "Dữ liệu giả lập, không dùng để chẩn đoán.");
            var v105 = NewVisit(p105, dentist1, dentist1, VisitStatuses.Completed,
                UtcAt(fixtureDate, new(9, 30)), UtcAt(fixtureDate, new(10, 30)), room1?.Id, true, UtcAt(fixtureDate, new(9, 30)), "NK006", "Dữ liệu giả lập, chỉ phục vụ cảnh báo trên giao diện.");
            var v106 = NewVisit(p106, dentist2, dentist1, VisitStatuses.InProgress,
                now.AddMinutes(-30), null, room2?.Id, false, now.AddHours(-1), "NK017", "Ca giả lập được tiếp nhận sau bàn giao.");
            var v107 = NewVisit(p107, dentist1, dentist1, VisitStatuses.Completed,
                UtcAt(today.AddDays(-1), new(14, 0)), UtcAt(today.AddDays(-1), new(14, 20)), room1?.Id, true, UtcAt(today.AddDays(-1), new(14, 0)), "NK002", "Dữ liệu giả lập.");

            db.Visits.AddRange(counterVisit, v101, v101Open, v103, v103Complete, v105, v106, v107);
            await db.SaveChangesAsync(ct);

            var appointments = AddAppointments(db, p101, p103, p105, p107, receptionist, today, fixtureDate, dueDate, dueSlot, ct);
            await db.SaveChangesAsync(ct);

            var nextTodayQueue = await NextQueueNumberAsync(db, today, ct);
            var nextDueQueue = dueDate == today ? nextTodayQueue++ : await NextQueueNumberAsync(db, dueDate, ct);
            var nextFixtureQueue = await NextQueueNumberAsync(db, fixtureDate, ct);
            var nextYesterdayQueue = await NextQueueNumberAsync(db, today.AddDays(-1), ct);
            if (dueDate == today.AddDays(-1)) nextYesterdayQueue++;
            var queueCounter = NewQueue(p101, appointments.Counter, counterVisit, dueDate, nextDueQueue++,
                dueIsToday ? QueueStatus.Waiting : QueueStatus.Completed,
                dueIsToday ? [History(null, QueueStatus.Waiting, receptionist.UserId, UtcAt(dueDate, dueSlot), "Tiếp đón tại quầy")]
                    : [History(null, QueueStatus.Waiting, receptionist.UserId, UtcAt(dueDate, dueSlot), "Tiếp đón tại quầy"), History(QueueStatus.Waiting, QueueStatus.InConsultation, dentist1.UserId, UtcAt(dueDate, dueSlot).AddMinutes(5), "Bắt đầu khám"), History(QueueStatus.InConsultation, QueueStatus.Completed, dentist1.UserId, UtcAt(dueDate, dueSlot).AddMinutes(30), "Hoàn tất ca giả lập")]);
            var queueChild = NewQueue(p103, null, v103, today, nextTodayQueue++, QueueStatus.Waiting,
            [
                History(null, QueueStatus.Waiting, receptionist.UserId, now.AddMinutes(-20), "Tiếp đón tại quầy"),
                History(QueueStatus.Waiting, QueueStatus.InImaging, dentist1.UserId, now.AddMinutes(-15), "Chuyển chụp theo chỉ định giả lập"),
                History(QueueStatus.InImaging, QueueStatus.Waiting, assistant.UserId, now.AddMinutes(-5), "Hoàn tất chụp, quay lại hàng đợi"),
            ]);
            var queue101 = NewQueue(p101, appointments.Completed101, v101, fixtureDate, nextFixtureQueue++, QueueStatus.Completed,
            [History(null, QueueStatus.Waiting, receptionist.UserId, UtcAt(fixtureDate, new(7, 50)), "Tiếp đón"), History(QueueStatus.Waiting, QueueStatus.InConsultation, dentist1.UserId, UtcAt(fixtureDate, new(8, 0)), "Bắt đầu khám"), History(QueueStatus.InConsultation, QueueStatus.Completed, dentist1.UserId, UtcAt(fixtureDate, new(9, 0)), "Hoàn tất ca khám")]);
            var queue103 = NewQueue(p103, appointments.Completed103, v103Complete, fixtureDate, nextFixtureQueue++, QueueStatus.Completed,
            [History(null, QueueStatus.Waiting, receptionist.UserId, UtcAt(fixtureDate, new(10, 50)), "Tiếp đón"), History(QueueStatus.Waiting, QueueStatus.InConsultation, dentist1.UserId, UtcAt(fixtureDate, new(11, 0)), "Bắt đầu khám"), History(QueueStatus.InConsultation, QueueStatus.Completed, dentist1.UserId, UtcAt(fixtureDate, new(11, 20)), "Hoàn tất ca khám")]);
            var queue106 = NewQueue(p106, null, v106, today, nextTodayQueue++, QueueStatus.InConsultation,
            [
                History(null, QueueStatus.Waiting, receptionist.UserId, now.AddHours(-2), "Tiếp đón"),
                History(QueueStatus.Waiting, QueueStatus.InConsultation, dentist1.UserId, now.AddHours(-1), "Bắt đầu khám"),
                History(QueueStatus.InConsultation, QueueStatus.Paused, dentist1.UserId, now.AddMinutes(-45), "Tạm dừng để bàn giao ca"),
                History(QueueStatus.Paused, QueueStatus.InConsultation, dentist2.UserId, now.AddMinutes(-30), "Nha sĩ tiếp nhận tiếp tục cùng Visit"),
            ]);
            var queue105 = NewQueue(p105, appointments.Completed105, v105, fixtureDate, nextFixtureQueue++, QueueStatus.Completed,
            [History(null, QueueStatus.Waiting, receptionist.UserId, UtcAt(fixtureDate, new(9, 20)), "Tiếp đón"), History(QueueStatus.Waiting, QueueStatus.InConsultation, dentist1.UserId, UtcAt(fixtureDate, new(9, 30)), "Bắt đầu khám"), History(QueueStatus.InConsultation, QueueStatus.Completed, dentist1.UserId, UtcAt(fixtureDate, new(10, 30)), "Hoàn tất ca khám")]);
            var queue107 = NewQueue(p107, appointments.Completed107, v107, today.AddDays(-1), nextYesterdayQueue, QueueStatus.Completed,
            [History(null, QueueStatus.Waiting, receptionist.UserId, UtcAt(today.AddDays(-1), new(13, 50)), "Tiếp đón"), History(QueueStatus.Waiting, QueueStatus.InConsultation, dentist1.UserId, UtcAt(today.AddDays(-1), new(14, 0)), "Bắt đầu khám"), History(QueueStatus.InConsultation, QueueStatus.Completed, dentist1.UserId, UtcAt(today.AddDays(-1), new(14, 20)), "Hoàn tất ca khám")]);
            db.QueueEntries.AddRange(queueCounter, queueChild, queue101, queue103, queue106, queue105, queue107);

            AddClinicalData(db, p101, p103, p105, v101, v101Open, v103, v103Complete, v105, assistant, now);
            await db.SaveChangesAsync(ct);

            var services = await CreateServicesAsync(db, v101, v103Complete, v105, v107, dentist1, now, ct);
            await db.SaveChangesAsync(ct);

            var medicineRows = await PrepareMedicinesAsync(db, now, admin.UserId, ct);
            var prescriptionCodes = await SeedPrescriptionsAsync(db, v101, v105, v106, dentist1, dentist2, medicineRows, fixtureDate, now, ct);
            await db.SaveChangesAsync(ct);

            var invoices = await SeedInvoicesAsync(db, v101, v103Complete, v105, v107, services, prescriptionCodes, receptionist, fixtureDate, ct);
            await db.SaveChangesAsync(ct);
            AddPayments(db, invoices, receptionist, fixtureDate);
            await db.SaveChangesAsync(ct);

            await SeedSuppliesAsync(db, v101, assistant, admin.UserId, now, ct);
            await SeedImagesAsync(db, imageStorage, contentRoot, storagePaths,
                [(p103, v103, 55, DentalImageCategory.Periapical, "anh-demo-xquang-test-only.png"),
                 (p101, v101Open, 16, DentalImageCategory.Intraoral, "anh-demo-noi-mieng-test-only.png")],
                dentist1, ct);

            db.AuditLogs.Add(new AuditLog
            {
                Action = MarkerAction,
                EntityType = MarkerType,
                Detail = MarkerDetail,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Patient test fixtures: seeded 7 patient profiles, pending online booking requests, and linked demo records.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            foreach (var path in storagePaths)
            {
                try { await imageStorage.RemoveAsync(path); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
            throw;
        }
    }

    private static async Task<bool> HasSeedMarkerAsync(DentalDbContext db, CancellationToken ct) =>
        await db.AuditLogs.AnyAsync(x => x.EntityType == MarkerType && x.Action == MarkerAction && x.Detail == MarkerDetail, ct);

    private sealed record SourcePerson(string SourceCode, string Name, DateOnly DateOfBirth, string Gender, string Phone, string Address, string? CitizenId);

    private static async Task SeedSourcePatientProfilesAsync(
        DentalDbContext db, IKeyedIdentityHasher hasher, string contentRoot, ILogger logger, CancellationToken ct)
    {
        if (await db.AuditLogs.AnyAsync(x => x.EntityType == SourceMarkerType && x.Action == MarkerAction && x.Detail == SourceMarkerDetail, ct)) return;

        var repositoryRoot = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", ".."));
        var sourcePath = Path.Combine(repositoryRoot, "docs", "TT_Benh_nhan_test", "TT_Benh_nhan_test_demo.md");
        if (!File.Exists(sourcePath))
        {
            logger.LogWarning("Patient source fixtures: source Markdown file not found; skipped.");
            return;
        }

        var people = (await File.ReadAllLinesAsync(sourcePath, ct))
            .Select(ParseSourcePerson)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray();
        if (people.Length != 100 || people.Select(x => x.SourceCode).Distinct(StringComparer.Ordinal).Count() != 100)
        {
            logger.LogWarning("Patient source fixtures: expected 100 unique source rows, found {Count}; skipped.", people.Length);
            return;
        }

        var hashedPeople = people.Select(person => (Person: person, IdentityHash: person.CitizenId is null ? null : hasher.Hash(person.CitizenId))).ToArray();
        var nonNullHashes = hashedPeople.Where(x => x.IdentityHash is not null).Select(x => x.IdentityHash!).ToArray();
        if (nonNullHashes.Distinct(StringComparer.Ordinal).Count() != nonNullHashes.Length)
        {
            logger.LogWarning("Patient source fixtures: duplicate identity hashes in source rows; skipped.");
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (await db.AuditLogs.AnyAsync(x => x.EntityType == SourceMarkerType && x.Action == MarkerAction && x.Detail == SourceMarkerDetail, ct))
            {
                await transaction.CommitAsync(ct);
                return;
            }

            foreach (var row in hashedPeople)
            {
                var patient = row.IdentityHash is null
                    ? await db.Patients.FirstOrDefaultAsync(x => x.FullName == row.Person.Name && x.DateOfBirth == row.Person.DateOfBirth && x.Phone == row.Person.Phone, ct)
                    : await db.Patients.FirstOrDefaultAsync(x => x.CitizenIdHash == row.IdentityHash, ct);
                if (patient is not null) continue;

                db.Patients.Add(new Patient
                {
                    FullName = row.Person.Name,
                    DateOfBirth = row.Person.DateOfBirth,
                    Gender = row.Person.Gender,
                    Phone = row.Person.Phone,
                    Address = row.Person.Address,
                    CitizenIdHash = row.IdentityHash,
                    CitizenIdMasked = row.Person.CitizenId is null ? null : hasher.Mask(row.Person.CitizenId),
                    DocumentType = row.Person.CitizenId is null ? null : IdentityDocumentType.CitizenId,
                    HashScheme = row.Person.CitizenId is null ? null : IdentityHashScheme.HmacCanonical,
                    IdentityKeyVersion = row.Person.CitizenId is null ? null : hasher.CurrentVersion,
                    CitizenIdVersion = 1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });
            }

            db.AuditLogs.Add(new AuditLog
            {
                Action = MarkerAction,
                EntityType = SourceMarkerType,
                Detail = SourceMarkerDetail,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Patient source fixtures: imported 100 source patient profiles with identity values stored as keyed hashes.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private static SourcePerson? ParseSourcePerson(string line)
    {
        if (!line.StartsWith('|') || !line.Contains("**KH-", StringComparison.Ordinal)) return null;
        var cells = line.Split('|').Skip(1).Take(10).Select(x => x.Trim()).ToArray();
        if (cells.Length != 10) return null;

        var sourceCode = Regex.Match(cells[0], @"KH-\d{3}").Value;
        var fullName = StripMarkdown(cells[2]);
        var dateText = Regex.Match(cells[3], @"\d{2}/\d{2}/\d{4}").Value;
        var phone = Regex.Match(cells[5], @"0\d{9,10}").Value;
        var genderText = StripMarkdown(cells[4]);
        var citizenId = Regex.Match(cells[1], @"(?<!\d)\d{12}(?!\d)").Value;
        if (sourceCode.Length == 0 || fullName.Length == 0 || phone.Length == 0 || dateText.Length == 0 ||
            !DateOnly.TryParseExact(dateText, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOfBirth)) return null;

        var gender = genderText.StartsWith("Nam", StringComparison.OrdinalIgnoreCase) ? "Nam"
            : genderText.StartsWith("Nữ", StringComparison.OrdinalIgnoreCase) ? "Nữ" : null;
        if (gender is null) return null;

        return new SourcePerson(sourceCode, fullName, dateOfBirth, gender, phone, StripMarkdown(cells[6]), citizenId.Length == 0 ? null : citizenId);
    }

    private static string StripMarkdown(string value) => value.Replace("**", string.Empty, StringComparison.Ordinal)
        .Replace("*", string.Empty, StringComparison.Ordinal)
        .Replace("`", string.Empty, StringComparison.Ordinal)
        .Trim();

    private static async Task<Dictionary<string, Patient>> SeedPeopleAsync(
        DentalDbContext db, IKeyedIdentityHasher hasher, int patientRoleId, string demoPassword, DateOnly today, DateTime now, CancellationToken ct)
    {
        var cutoff = PatientAccountAge.LatestEligibleBirthDate(today);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(demoPassword, 12);
        var result = new Dictionary<string, Patient>(StringComparer.Ordinal);
        foreach (var seed in People)
        {
            var identityHash = seed.CitizenId is null ? null : hasher.Hash(seed.CitizenId);
            var patient = seed.CitizenId is null
                ? await db.Patients.FirstOrDefaultAsync(x => x.FullName == seed.Name && x.DateOfBirth == seed.DateOfBirth && x.Phone == seed.Phone, ct)
                : await db.Patients.FirstOrDefaultAsync(x => x.CitizenIdHash == identityHash, ct);

            if (patient is null)
            {
                patient = new Patient
                {
                    FullName = seed.Name,
                    DateOfBirth = seed.DateOfBirth,
                    Gender = seed.Gender,
                    Phone = seed.Phone,
                    CitizenIdHash = identityHash,
                    CitizenIdMasked = seed.CitizenId is null ? null : hasher.Mask(seed.CitizenId),
                    DocumentType = seed.CitizenId is null ? null : IdentityDocumentType.CitizenId,
                    HashScheme = seed.CitizenId is null ? null : IdentityHashScheme.HmacCanonical,
                    IdentityKeyVersion = seed.CitizenId is null ? null : hasher.CurrentVersion,
                    CitizenIdVersion = 1,
                    IsActive = true,
                    CreatedAt = now.AddDays(-10),
                };
                db.Patients.Add(patient);
            }

            if (seed.HasAccount && seed.CitizenId is not null && patient.DateOfBirth <= cutoff && patient.UserId is null)
            {
                var user = await db.Users.FirstOrDefaultAsync(x => x.LoginByCitizenId && x.Phone == seed.Phone && x.FullName == seed.Name && x.RoleId == patientRoleId, ct);
                if (user is null)
                {
                    user = new User
                    {
                        Phone = seed.Phone,
                        LoginByCitizenId = true,
                        PasswordHash = passwordHash,
                        FullName = seed.Name,
                        DateOfBirth = seed.DateOfBirth,
                        Gender = seed.Gender,
                        RoleId = patientRoleId,
                        IsActive = seed.AccountActive,
                        RequiresPhoneActivation = seed.RequiresActivation,
                        PhoneActivationReason = seed.RequiresActivation ? PhoneActivationReason.InitialActivation : null,
                        PhoneVerifiedAtUtc = seed.RequiresActivation ? null : now,
                        CreatedAt = now.AddDays(-10),
                    };
                    db.Users.Add(user);
                }
                patient.User = user;
            }
            result.Add(seed.Key, patient);
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    private static async Task AddOnlineBookingIfMissingAsync(
        DentalDbContext db, string key, string name, string phone, DateOnly dob, string gender, DateOnly date, TimeOnly slot, CancellationToken ct)
    {
        var requestId = key switch
        {
            "REQ-ONLINE-01" => Guid.Parse("ea897090-2026-4dcb-8fa1-000000000101"),
            _ => Guid.Parse("ea897090-2026-4dcb-8fa1-000000000102"),
        };
        if (await db.BookingRequests.AnyAsync(x => x.ClientRequestId == requestId, ct)) return;
        db.BookingRequests.Add(new BookingRequest
        {
            ClientRequestId = requestId,
            IntentDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("patient-test-fixture-v1:" + key))),
            FullName = name,
            Phone = phone,
            DateOfBirth = dob,
            Gender = gender,
            Notes = "Yêu cầu đặt lịch giả lập, chưa xác minh tại quầy.",
            AppointmentDate = date,
            SlotTime = slot,
            SlotDurationMinutes = 30,
            Status = "Pending",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
        });
    }

    private sealed record AppointmentSet(Appointment Counter, Appointment Completed101, Appointment Completed103, Appointment Completed105, Appointment Completed107);

    private static AppointmentSet AddAppointments(
        DentalDbContext db, Patient p101, Patient p103, Patient p105, Patient p107, User receptionist,
        DateOnly today, DateOnly fixtureDate, DateOnly dueDate, TimeOnly dueSlot, CancellationToken ct)
    {
        _ = ct;
        var dueToday = dueDate == today;
        var appointments = new AppointmentSet(
            new Appointment { PatientId = p101.PatientId, AppointmentDate = dueDate, SlotTime = dueSlot, SlotDurationMinutes = 30, Status = dueToday ? AppointmentStatuses.CheckedIn : AppointmentStatuses.Completed, Notes = "[APT-DESK-NOW-01] Lịch đặt tại quầy giả lập.", CreatedByUserId = receptionist.UserId, CreatedAt = UtcAt(dueDate, dueSlot) },
            new Appointment { PatientId = p101.PatientId, AppointmentDate = fixtureDate, SlotTime = new(8, 0), SlotDurationMinutes = 30, Status = AppointmentStatuses.Completed, Notes = "[VIS-TST-101-01] Lịch hoàn tất giả lập.", CreatedByUserId = receptionist.UserId, CreatedAt = UtcAt(fixtureDate, new(7, 45)) },
            new Appointment { PatientId = p103.PatientId, AppointmentDate = fixtureDate, SlotTime = new(11, 0), SlotDurationMinutes = 30, Status = AppointmentStatuses.Completed, Notes = "[VIS-TST-103-02] Lịch hoàn tất giả lập.", CreatedByUserId = receptionist.UserId, CreatedAt = UtcAt(fixtureDate, new(10, 45)) },
            new Appointment { PatientId = p105.PatientId, AppointmentDate = fixtureDate, SlotTime = new(9, 30), SlotDurationMinutes = 30, Status = AppointmentStatuses.Completed, Notes = "[INV-TST-105-PARTIAL] Lịch hoàn tất giả lập.", CreatedByUserId = receptionist.UserId, CreatedAt = UtcAt(fixtureDate, new(9, 15)) },
            new Appointment { PatientId = p107.PatientId, AppointmentDate = today.AddDays(-1), SlotTime = new(14, 0), SlotDurationMinutes = 30, Status = AppointmentStatuses.Completed, Notes = "[VIS-TST-107-01] Lịch hoàn tất giả lập.", CreatedByUserId = receptionist.UserId, CreatedAt = UtcAt(today.AddDays(-1), new(13, 45)) });
        db.Appointments.AddRange(appointments.Counter, appointments.Completed101, appointments.Completed103, appointments.Completed105, appointments.Completed107);
        db.Appointments.Add(new Appointment
        {
            PatientId = p107.PatientId,
            AppointmentDate = today.AddDays(2),
            SlotTime = new(10, 30),
            SlotDurationMinutes = 30,
            Status = AppointmentStatuses.Scheduled,
            Notes = "[APT-DESK-NOT-DUE-01] Lịch tương lai giả lập.",
            CreatedByUserId = receptionist.UserId,
            CreatedAt = DateTime.UtcNow,
        });
        db.Appointments.Add(new Appointment
        {
            PatientId = p105.PatientId,
            AppointmentDate = today.AddDays(2),
            SlotTime = new(9, 0),
            SlotDurationMinutes = 30,
            Status = AppointmentStatuses.Scheduled,
            Notes = "[APT-DESK-LATER-01] Lịch tương lai giả lập để thử nhắc dư nợ.",
            CreatedByUserId = receptionist.UserId,
            CreatedAt = DateTime.UtcNow,
        });
        return appointments;
    }

    private static Visit NewVisit(Patient patient, User createdBy, User? dentist, string status, DateTime? started, DateTime? ended,
        int? roomId, bool locked, DateTime createdAt, string? diagnosis = null, string? notes = null) => new()
    {
        PatientId = patient.PatientId,
        Status = status,
        StartedAt = started,
        EndedAt = ended,
        DentistId = dentist?.UserId,
        ChairId = null,
        RoomId = roomId,
        CreatedByUserId = createdBy.UserId,
        Diagnosis = diagnosis,
        ClinicalNotes = notes,
        IsLocked = locked,
        LockedAt = locked ? ended : null,
        LockedBy = locked ? dentist?.UserId : null,
        CreatedAt = createdAt,
    };

    private static QueueEntry NewQueue(Patient patient, Appointment? appointment, Visit visit, DateOnly date, int number, QueueStatus status,
        List<QueueStatusHistory> histories) => new()
    {
        PatientId = patient.PatientId,
        AppointmentId = appointment?.AppointmentId,
        VisitId = visit.VisitId,
        QueueDate = date,
        QueueNumber = number,
        IsPriority = false,
        CheckInTime = histories[0].ChangedAt,
        Status = status,
        Notes = "Lượt kiểm thử giả lập.",
        CreatedAt = histories[0].ChangedAt,
        StatusHistories = histories,
    };

    private static QueueStatusHistory History(QueueStatus? from, QueueStatus to, int actor, DateTime at, string reason) => new()
    {
        FromStatus = from,
        ToStatus = to,
        ChangedByUserId = actor,
        ChangedAt = at,
        Reason = reason,
    };

    private static async Task<int> NextQueueNumberAsync(DentalDbContext db, DateOnly date, CancellationToken ct) =>
        await db.QueueEntries.Where(x => x.QueueDate == date).Select(x => (int?)x.QueueNumber).MaxAsync(ct) is int max ? max + 1 : 1;

    private static void AddClinicalData(
        DentalDbContext db, Patient p101, Patient p103, Patient p105, Visit v101, Visit v101Open, Visit v103, Visit v103Complete, Visit v105,
        User assistant, DateTime now)
    {
        db.MedicalHistoryRecords.Add(new MedicalHistoryRecord
        {
            PatientId = p105.PatientId,
            VisitId = v105.VisitId,
            RecordedByUserId = assistant.UserId,
            Note = "Bản ghi tiền sử giả lập để thử banner cảnh báo.",
            CreatedAt = v105.StartedAt!.Value,
            Items =
            [
                new MedicalHistoryItem { Type = MedicalHistoryTypes.Allergy, Name = "Penicillin", IsCritical = true, Detail = "Dữ liệu dị ứng giả lập, chỉ dùng kiểm tra giao diện." },
                new MedicalHistoryItem { Type = MedicalHistoryTypes.Condition, Name = "Tăng huyết áp", IsCritical = true, Detail = "Dữ liệu bệnh nền giả lập, chỉ dùng kiểm tra giao diện." },
            ],
        });
        db.VitalSignRecords.Add(new VitalSignRecord
        {
            PatientId = p105.PatientId, VisitId = v105.VisitId, SystolicBp = 158, DiastolicBp = 96,
            PulseBpm = 88, TemperatureC = 36.7m, Note = "Sinh hiệu giả lập phục vụ kiểm thử banner.",
            RecordedByUserId = assistant.UserId, CreatedAt = v105.StartedAt.Value,
        });
        db.VitalSignRecords.Add(new VitalSignRecord
        {
            PatientId = p101.PatientId, VisitId = v101.VisitId, SystolicBp = 120, DiastolicBp = 80,
            PulseBpm = 72, TemperatureC = 36.6m, Note = "Sinh hiệu giả lập bình thường.",
            RecordedByUserId = assistant.UserId, CreatedAt = v101.StartedAt!.Value,
        });

        db.ToothConditions.AddRange(
            new ToothCondition { VisitId = v101.VisitId, ToothNumber = 36, Surface = "O", ConditionCode = "CARIES", Note = "Tình trạng ban đầu giả lập.", CreatedAt = v101.StartedAt!.Value.AddMinutes(10) },
            new ToothCondition { VisitId = v101.VisitId, ToothNumber = 36, Surface = "O", ConditionCode = "RESTORED", Note = "Bản ghi sau điều trị giả lập.", CreatedAt = v101.EndedAt!.Value },
            new ToothCondition { VisitId = v101Open.VisitId, ToothNumber = 16, Surface = "M", ConditionCode = "CARIES", Note = "Tình trạng lần khám mới giả lập.", CreatedAt = now },
            new ToothCondition { VisitId = v103.VisitId, ToothNumber = 55, Surface = "O", ConditionCode = "CARIES", Note = "Răng sữa, dữ liệu giả lập.", CreatedAt = now },
            new ToothCondition { VisitId = v103.VisitId, ToothNumber = 75, Surface = "O", ConditionCode = "CARIES", Note = "Răng sữa, dữ liệu giả lập.", CreatedAt = now },
            new ToothCondition { VisitId = v103Complete.VisitId, ToothNumber = 55, Surface = "O", ConditionCode = "RESTORED", Note = "Bản ghi điều trị giả lập.", CreatedAt = v103Complete.EndedAt!.Value },
            new ToothCondition { VisitId = v105.VisitId, ToothNumber = 26, Surface = "O", ConditionCode = "CARIES", Note = "Dữ liệu lâm sàng giả lập.", CreatedAt = v105.StartedAt!.Value.AddMinutes(10) });
    }

    private sealed record ServiceSet(VisitService S101Cleaning, VisitService S101Composite, VisitService S103, VisitService S105Implant, VisitService S105Consult, VisitService S107Cleaning);

    private static async Task<ServiceSet> CreateServicesAsync(DentalDbContext db, Visit v101, Visit v103, Visit v105, Visit v107, User dentist, DateTime now, CancellationToken ct)
    {
        var catalog = await db.DentalServices.Include(x => x.Prices).ToDictionaryAsync(x => x.Code, ct);
        VisitService Make(string code, Visit visit, int? tooth, string? surface, DateTime createdAt)
        {
            var service = catalog[code];
            var price = service.Prices.OrderByDescending(x => x.EffectiveFrom).First();
            return new VisitService
            {
                VisitId = visit.VisitId, ServiceId = service.DentalServiceId, ServicePriceId = price.ServicePriceId,
                ServiceCode = service.Code, ServiceName = service.Name, ToothNumber = tooth, Surface = surface,
                Quantity = 1, UnitPrice = price.Amount, CreatedByUserId = dentist.UserId, CreatedAt = createdAt,
            };
        }
        var result = new ServiceSet(
            Make("DV002", v101, null, null, v101.StartedAt!.Value),
            Make("DV003", v101, 36, "O", v101.StartedAt!.Value.AddMinutes(10)),
            Make("DV001", v103, 55, "O", v103.EndedAt!.Value),
            Make("DV005", v105, 26, "O", v105.StartedAt!.Value),
            Make("DV007", v105, null, null, v105.StartedAt!.Value.AddMinutes(5)),
            Make("DV002", v107, null, null, v107.EndedAt!.Value));
        db.VisitServices.AddRange(result.S101Cleaning, result.S101Composite, result.S103, result.S105Implant, result.S105Consult, result.S107Cleaning);
        return result;
    }

    private static async Task<(Medicine Med011, Medicine Med030)> PrepareMedicinesAsync(DentalDbContext db, DateTime now, int adminId, CancellationToken ct)
    {
        var med011 = await db.Medicines.SingleAsync(x => x.Code == "MED011", ct);
        var med030 = await db.Medicines.SingleAsync(x => x.Code == "MED030", ct);
        med011.UnitPrice = 2000m;
        med011.IsActive = true;
        med030.IsActive = true;

        if (!await db.MedicineStockBalances.AnyAsync(x => x.MedicineId == med011.Id, ct))
        {
            db.MedicineStockReceipts.Add(new MedicineStockReceipt
            {
                RequestId = FixtureGuid("MED011-BASE-RECEIPT"), PayloadHash = HashText("MED011-BASE-RECEIPT"),
                MedicineId = med011.Id, MedicineCode = med011.Code, MedicineName = med011.Name, Unit = med011.Unit,
                Quantity = 500, SupplierName = "Nhà cung cấp giả lập", Notes = "Tồn ban đầu dữ liệu Development.",
                ConfirmedByUserId = adminId, ConfirmedAt = now.AddDays(-7),
            });
            db.MedicineStockBalances.Add(new MedicineStockBalance { MedicineId = med011.Id, OnHand = 500, Version = 1, UpdatedAt = now });
        }
        if (!await db.MedicineStockBalances.AnyAsync(x => x.MedicineId == med030.Id, ct))
            db.MedicineStockBalances.Add(new MedicineStockBalance { MedicineId = med030.Id, OnHand = 0, Version = 1, UpdatedAt = now });
        return (med011, med030);
    }

    private sealed record PrescriptionSet(Prescription Rx101, Prescription Rx105, Prescription Rx106Old, Prescription Rx106New);

    private static async Task<PrescriptionSet> SeedPrescriptionsAsync(
        DentalDbContext db, Visit v101, Visit v105, Visit v106, User dentist1, User dentist2,
        (Medicine Med011, Medicine Med030) meds, DateOnly date, DateTime now, CancellationToken ct)
    {
        var counter = await db.PrescriptionNumberCounters.SingleOrDefaultAsync(x => x.PrescriptionDate == date, ct);
        var last = counter?.LastNumber ?? 0;
        var existingCodes = await db.Prescriptions.Where(x => x.PrescriptionCode.StartsWith($"RX-{date:yyyyMMdd}-")).Select(x => x.PrescriptionCode).ToListAsync(ct);
        last = Math.Max(last, MaxCodeNumber(existingCodes));
        string NextCode() => $"RX-{date:yyyyMMdd}-{++last:D4}";

        Prescription Make(Visit visit, User dentist, Medicine medicine, DateTime createdAt, bool cancelled = false) => new()
        {
            VisitId = visit.VisitId,
            DoctorId = dentist.UserId,
            PrescriptionCode = NextCode(),
            Diagnosis = "DỮ LIỆU KIỂM THỬ - KHÔNG DÙNG ĐIỀU TRỊ",
            DoctorAdvice = "Đơn giả lập; không sử dụng cho bệnh nhân.",
            IsCancelled = cancelled,
            CreatedAt = createdAt,
            CreatedByUserId = dentist.UserId,
            Version = 1,
            Items =
            [
                new PrescriptionItem
                {
                    MedicineId = medicine.Id, MedicineCode = medicine.Code, MedicineName = medicine.Name,
                    ActiveIngredient = medicine.ActiveIngredient, Strength = medicine.Strength,
                    Unit = medicine.Unit, Quantity = 5, UnitPrice = medicine.UnitPrice,
                    UsageInstructions = "CHỈ DÙNG DỮ LIỆU TEST - KHÔNG SỬ DỤNG",
                },
            ],
        };

        var rx101 = Make(v101, dentist1, meds.Med011, v101.EndedAt!.Value.AddMinutes(-5));
        var rx105 = Make(v105, dentist1, meds.Med011, v105.EndedAt!.Value.AddMinutes(-5));
        var rxOld = Make(v106, dentist1, meds.Med011, now.AddHours(-1), cancelled: true);
        var rxNew = Make(v106, dentist2, meds.Med030, now.AddMinutes(-25));
        db.Prescriptions.AddRange(rx101, rx105, rxOld, rxNew);
        if (counter is null) db.PrescriptionNumberCounters.Add(new PrescriptionNumberCounter { PrescriptionDate = date, LastNumber = last });
        else counter.LastNumber = last;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = dentist2.UserId, Action = "PRESCRIPTION_CANCELLED", EntityType = "Prescription",
            Detail = "{\"reason\":\"test fixture replacement\"}", CreatedAt = now,
        });
        return new PrescriptionSet(rx101, rx105, rxOld, rxNew);
    }

    private sealed record InvoiceSet(Invoice Paid101, Invoice Partial105, Invoice Draft103, Invoice Cancelled107);

    private static async Task<InvoiceSet> SeedInvoicesAsync(
        DentalDbContext db, Visit v101, Visit v103, Visit v105, Visit v107, ServiceSet services,
        PrescriptionSet prescriptions, User cashier, DateOnly date, CancellationToken ct)
    {
        var counter = await db.InvoiceNumberCounters.SingleOrDefaultAsync(x => x.InvoiceDate == date, ct);
        var last = counter?.LastNumber ?? 0;
        var existingCodes = await db.Invoices.Where(x => x.InvoiceCode.StartsWith($"INV-{date:yyyyMMdd}-")).Select(x => x.InvoiceCode).ToListAsync(ct);
        last = Math.Max(last, MaxCodeNumber(existingCodes));
        string NextCode() => $"INV-{date:yyyyMMdd}-{++last:D4}";
        var fix50 = await db.DiscountPresets.FirstOrDefaultAsync(x => x.Code == "FIX50", ct);
        var rx101Item = prescriptions.Rx101.Items.Single();
        var rx105Item = prescriptions.Rx105.Items.Single();

        Invoice Make(Visit visit, InvoiceStatus status, DateTime created, List<InvoiceItem> items, decimal? discount = null) => new()
        {
            VisitId = visit.VisitId,
            InvoiceCode = NextCode(),
            Status = status,
            TotalAmount = items.Sum(x => x.TotalAmount),
            PaidAmount = 0,
            DiscountAmount = discount ?? 0,
            DiscountType = discount.HasValue ? "FixedAmount" : null,
            DiscountValue = discount,
            DiscountPresetId = discount.HasValue ? fix50?.Id : null,
            DiscountReason = discount.HasValue ? "Mẫu giảm FIX50 cho dữ liệu kiểm thử." : null,
            CreatedByUserId = cashier.UserId,
            CreatedAt = created,
            Items = items,
        };

        var paid = Make(v101, InvoiceStatus.Paid, v101.EndedAt!.Value,
        [
            ServiceInvoiceItem(services.S101Cleaning),
            ServiceInvoiceItem(services.S101Composite),
            MedicineInvoiceItem(rx101Item),
        ], 50000m);
        paid.PaidAmount = paid.TotalAmount - paid.DiscountAmount;

        var partial = Make(v105, InvoiceStatus.PartiallyPaid, v105.EndedAt!.Value,
        [ServiceInvoiceItem(services.S105Implant), ServiceInvoiceItem(services.S105Consult), MedicineInvoiceItem(rx105Item)]);
        partial.PaidAmount = 900000m;

        var draft = Make(v103, InvoiceStatus.PendingPayment, v103.EndedAt!.Value, [ServiceInvoiceItem(services.S103)]);
        var cancelled = Make(v107, InvoiceStatus.Cancelled, v107.EndedAt!.Value, [ServiceInvoiceItem(services.S107Cleaning)]);

        db.Invoices.AddRange(paid, partial, draft, cancelled);
        prescriptions.Rx101.Invoice = paid;
        prescriptions.Rx105.Invoice = partial;
        if (counter is null) db.InvoiceNumberCounters.Add(new InvoiceNumberCounter { InvoiceDate = date, LastNumber = last });
        else counter.LastNumber = last;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = cashier.UserId, Action = "INVOICE_DRAFT_CANCELLED", EntityType = "Invoice",
            Detail = "{\"reason\":\"Dữ liệu kiểm thử, chưa thu tiền\"}", CreatedAt = DateTime.UtcNow,
        });
        return new InvoiceSet(paid, partial, draft, cancelled);
    }

    private static InvoiceItem ServiceInvoiceItem(VisitService service) => new()
    {
        SourceVisitServiceId = service.Id, ItemType = "Service", Code = service.ServiceCode, Name = service.ServiceName,
        ToothNumber = service.ToothNumber, Surface = service.Surface, Quantity = service.Quantity,
        UnitPrice = service.UnitPrice, TotalAmount = service.UnitPrice * service.Quantity,
    };

    private static InvoiceItem MedicineInvoiceItem(PrescriptionItem item) => new()
    {
        SourceVisitServiceId = 0, SourcePrescriptionItemId = item.Id, ItemType = "Medicine", Code = item.MedicineCode,
        Name = item.MedicineName, Quantity = item.Quantity, UnitPrice = item.UnitPrice, TotalAmount = item.TotalPrice,
    };

    private static void AddPayments(DentalDbContext db, InvoiceSet invoices, User cashier, DateOnly date)
    {
        var paidAt = UtcAt(date, new(12, 0));
        db.PaymentTransactions.AddRange(
            new PaymentTransaction
            {
                Invoice = invoices.Paid101, Amount = 510000m, AmountTendered = 510000m, ChangeAmount = 0m,
                PaymentMethod = "Cash", CashierId = cashier.UserId, PaidAt = paidAt,
                RequestId = FixtureGuid("INV-TST-101-CASH"), TransactionType = "Receipt", PrintCount = 0,
                Note = "Dữ liệu thanh toán giả lập Development.",
            },
            new PaymentTransaction
            {
                Invoice = invoices.Partial105, Amount = 500000m, AmountTendered = 500000m, ChangeAmount = 0m,
                PaymentMethod = "Cash", CashierId = cashier.UserId, PaidAt = paidAt.AddMinutes(10),
                RequestId = FixtureGuid("INV-TST-105-CASH"), TransactionType = "Receipt", PrintCount = 0,
                Note = "Đợt thu tiền mặt giả lập Development.",
            },
            new PaymentTransaction
            {
                Invoice = invoices.Partial105, Amount = 400000m, AmountTendered = 400000m, ChangeAmount = 0m,
                PaymentMethod = "BankTransfer", CashierId = cashier.UserId, PaidAt = paidAt.AddMinutes(20),
                RequestId = FixtureGuid("INV-TST-105-BANK"), TransactionType = "Receipt", PrintCount = 0,
                TransactionReference = "TEST-BANK-2026-001", Source = "ManualConfirm", BankReceivedAmount = 400000m,
                Note = "Chuyển khoản mẫu giả lập Development.",
            });
        db.AuditLogs.AddRange(
            new AuditLog { UserId = cashier.UserId, Action = "PAYMENT_CONFIRMED", EntityType = "Invoice", Detail = "{\"method\":\"Cash\",\"amount\":510000,\"fixture\":true}", CreatedAt = paidAt },
            new AuditLog { UserId = cashier.UserId, Action = "PAYMENT_CONFIRMED", EntityType = "Invoice", Detail = "{\"method\":\"Cash\",\"amount\":500000,\"fixture\":true}", CreatedAt = paidAt.AddMinutes(10) },
            new AuditLog { UserId = cashier.UserId, Action = "PAYMENT_CONFIRMED", EntityType = "Invoice", Detail = "{\"method\":\"BankTransfer\",\"amount\":400000,\"fixture\":true}", CreatedAt = paidAt.AddMinutes(20) });
    }

    private static async Task SeedSuppliesAsync(DentalDbContext db, Visit visit, User assistant, int adminId, DateTime now, CancellationToken ct)
    {
        var supplies = await db.MedicalSupplies.OrderBy(x => x.Code).ToListAsync(ct);
        if (!await db.StockReceipts.AnyAsync(ct))
        {
            var baseline = new StockReceipt
            {
                RequestId = FixtureGuid("SUPPLY-BASELINE"), PayloadHash = HashText("SUPPLY-BASELINE"),
                SupplierName = "Nhà cung cấp giả lập", Notes = "Tồn đầu kỳ test Development.",
                ConfirmedByUserId = adminId, ConfirmedAt = now.AddDays(-7),
                Lines = supplies.Select(x => new StockReceiptLine
                {
                    MedicalSupplyId = x.Id, SupplyCode = x.Code, SupplyName = x.Name, Unit = x.Unit,
                    Quantity = x.Code == "VT003" ? 3 : x.Code is "VT001" or "VT002" ? 50 : 25,
                }).ToList(),
            };
            db.StockReceipts.Add(baseline);
            await db.SaveChangesAsync(ct);
            foreach (var line in baseline.Lines)
                db.StockBalances.Add(new StockBalance { MedicalSupplyId = line.MedicalSupplyId, OnHand = line.Quantity, Version = 1, UpdatedAt = now });
            await db.SaveChangesAsync(ct);
        }

        var receiptSupply = supplies.SingleOrDefault(x => x.Code == "VT003");
        if (receiptSupply is not null)
        {
            var receipt = new StockReceipt
            {
                RequestId = FixtureGuid("RCV-TST-01"), PayloadHash = HashText("RCV-TST-01"),
                SupplierName = "Nhà cung cấp giả lập", Notes = "Phiếu nhập test-only, một hộp VT003.",
                ConfirmedByUserId = adminId, ConfirmedAt = now,
                Lines = [new StockReceiptLine { MedicalSupplyId = receiptSupply.Id, Quantity = 1, SupplyCode = receiptSupply.Code, SupplyName = receiptSupply.Name, Unit = receiptSupply.Unit }],
            };
            db.StockReceipts.Add(receipt);
            var balance = await db.StockBalances.SingleOrDefaultAsync(x => x.MedicalSupplyId == receiptSupply.Id, ct);
            if (balance is null) db.StockBalances.Add(new StockBalance { MedicalSupplyId = receiptSupply.Id, OnHand = 1, Version = 1, UpdatedAt = now });
            else { balance.OnHand += 1; balance.Version++; balance.UpdatedAt = now; }
            await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog { UserId = adminId, Action = "STOCK_RECEIPT_CONFIRMED", EntityType = "StockReceipt", EntityId = receipt.Id, Detail = "{\"fixture\":true,\"code\":\"VT003\",\"quantity\":1}", CreatedAt = now });
        }

        var issueLines = new List<StockIssueLine>();
        foreach (var code in new[] { "VT001", "VT005" })
        {
            var supply = supplies.SingleOrDefault(x => x.Code == code);
            if (supply is null) continue;
            var balance = await db.StockBalances.SingleOrDefaultAsync(x => x.MedicalSupplyId == supply.Id, ct);
            if (balance is null)
            {
                var received = await db.StockReceiptLines.Where(x => x.MedicalSupplyId == supply.Id).SumAsync(x => (int?)x.Quantity, ct) ?? 0;
                var issued = await db.StockIssueLines.Where(x => x.MedicalSupplyId == supply.Id).SumAsync(x => (int?)x.Quantity, ct) ?? 0;
                balance = new StockBalance { MedicalSupplyId = supply.Id, OnHand = received - issued, Version = 1, UpdatedAt = now };
                db.StockBalances.Add(balance);
            }
            if (balance.OnHand < 1) continue;
            issueLines.Add(new StockIssueLine { MedicalSupplyId = supply.Id, Quantity = 1, SupplyCode = supply.Code, SupplyName = supply.Name, Unit = supply.Unit });
            balance.OnHand -= 1;
            balance.Version++;
            balance.UpdatedAt = now;
        }
        if (issueLines.Count > 0)
        {
            var issue = new StockIssue
            {
                VisitId = visit.VisitId, RequestId = FixtureGuid("ISS-TST-101-01"), PayloadHash = HashText("ISS-TST-101-01"),
                Notes = "Tiêu hao thực tế giả lập, đã xác nhận.", ConfirmedByUserId = assistant.UserId, ConfirmedAt = now, Lines = issueLines,
            };
            db.StockIssues.Add(issue);
            await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog { UserId = assistant.UserId, Action = "STOCK_ISSUE_CONFIRMED", EntityType = "StockIssue", EntityId = issue.Id, Detail = "{\"fixture\":true,\"lineCount\":2}", CreatedAt = now });
        }
    }

    private static async Task SeedImagesAsync(
        DentalDbContext db, IDentalImageStorage storage, string contentRoot, List<string> writtenPaths,
        (Patient Patient, Visit Visit, int Tooth, DentalImageCategory Category, string FileName)[] images, User uploader, CancellationToken ct)
    {
        foreach (var (patient, visit, tooth, category, fileName) in images)
        {
            var fixtureRoot = FindFixtureRoot(contentRoot);
            var fullPath = Path.Combine(fixtureRoot, "docs", "TT_Benh_nhan_test", "assets", fileName);
            if (!File.Exists(fullPath)) continue;
            var bytes = await File.ReadAllBytesAsync(fullPath, ct);
            var dimensions = DentalImageFile.Inspect(fileName, "image/png", bytes);
            if (dimensions is null) continue;
            var storagePath = $"{patient.PatientId}/{Guid.NewGuid():N}.png";
            await storage.WriteAsync(storagePath, bytes, ct);
            writtenPaths.Add(storagePath);
            var image = new DentalImage
            {
                PatientId = patient.PatientId, VisitId = visit.VisitId, ToothNumber = tooth, Category = category,
                Notes = "Ảnh tổng hợp test-only; không phải phim bệnh nhân và không dùng để chẩn đoán.",
                OriginalFileName = fileName, StoragePath = storagePath, ContentType = "image/png", SizeBytes = bytes.LongLength,
                Width = dimensions.Value.Width, Height = dimensions.Value.Height, UploadedByUserId = uploader.UserId, CreatedAt = DateTime.UtcNow,
            };
            db.DentalImages.Add(image);
            await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog
            {
                UserId = uploader.UserId, Action = "MOD_IMG_UPLOADED", EntityType = "DentalImage", EntityId = image.Id,
                CreatedAt = DateTime.UtcNow,
                Detail = JsonSerializer.Serialize(new { image.VisitId, image.ToothNumber, image.Category, image.Version, image.SizeBytes, fixture = true }),
            });
        }
    }

    private static DateTime VietnamTime(DateTime utc)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
    }

    private static string FindFixtureRoot(string contentRoot)
    {
        for (var directory = new DirectoryInfo(contentRoot); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "docs", "TT_Benh_nhan_test", "assets")))
                return directory.FullName;
        }
        return contentRoot;
    }

    private static DateTime UtcAt(DateOnly date, TimeOnly time)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        return TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time), zone);
    }

    private static TimeOnly DueSlot(TimeSpan localTime)
    {
        var minutes = Math.Clamp(((int)localTime.TotalMinutes / 30) * 30, 8 * 60, 17 * 60);
        return new TimeOnly(minutes / 60, minutes % 60);
    }

    private static int MaxCodeNumber(IEnumerable<string> codes) => codes
        .Select(x => x.Split('-', StringSplitOptions.RemoveEmptyEntries).LastOrDefault())
        .Select(x => int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
        .DefaultIfEmpty(0)
        .Max();

    private static Guid FixtureGuid(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes("patient-test-fixture:" + value)).AsSpan(0, 16));
    private static string HashText(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("patient-test-fixture:" + value)));
}
