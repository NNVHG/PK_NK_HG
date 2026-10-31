using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

internal static class RoomDataMigrationSmoke
{
    private const string PreRoomMigration = "20261029041816_AddPhoneActivationReason";
    private const string RoomMigration = "20261031044130_AddIndependentClinicRooms";

    public static async Task RunAsync(
        DentalDbContext setup,
        DbContextOptions<DentalDbContext> options,
        Action<bool, string> check)
    {
        var dbName = setup.Database.GetDbConnection().Database;
        if (!dbName.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("RoomDataMigrationSmoke chỉ được chạy trên DB kiểm thử cô lập.");

        var migrator = setup.GetService<IMigrator>();

        // 1. Nâng tới migration ngay trước E1 (schema cũ chỉ có DentalChairs/StaffWorkspaces/Visits.ChairId)
        await migrator.MigrateAsync(PreRoomMigration);
        var appliedBefore = (await setup.Database.GetAppliedMigrationsAsync()).ToList();
        check(
            appliedBefore.Contains(PreRoomMigration) && !appliedBefore.Contains(RoomMigration),
            "Isolated test DB migrated to pre-E1 schema (20261029041816_AddPhoneActivationReason)");

        var conn = (NpgsqlConnection)setup.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        // Tạo dữ liệu Roles, Users, Patients, DentalChairs, StaffWorkspaces, Visits ở schema cũ bằng SQL
        int adminId, dentist1Id, dentist2Id, assistant1Id, assistant2Id, patient1Id, patient2Id;
        int chair1Id, chair2Id, chair3Id;
        int activeVisitId, completedVisit1Id, completedVisit2Id, cancelledVisitId;

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO "Roles" ("RoleCode", "RoleName", "CreatedAt")
                VALUES
                    ('ADMIN', 'Quản trị viên', NOW()),
                    ('DENTIST', 'Nha sĩ', NOW()),
                    ('ASSISTANT', 'Phụ tá', NOW());
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        async Task<int> InsertUserAsync(string roleCode, string fullName, string phone)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO "Users" (
                    "RoleId", "FullName", "Phone", "PasswordHash", "IsActive",
                    "RequiresPhoneActivation", "TokenVersion", "CreatedAt"
                )
                VALUES (
                    (SELECT "RoleId" FROM "Roles" WHERE "RoleCode" = @role),
                    @name, @phone, @hash, TRUE, FALSE, 0, NOW()
                )
                RETURNING "UserId";
                """;
            cmd.Parameters.AddWithValue("role", roleCode);
            cmd.Parameters.AddWithValue("name", fullName);
            cmd.Parameters.AddWithValue("phone", phone);
            cmd.Parameters.AddWithValue("hash", BCrypt.Net.BCrypt.HashPassword("SyntheticRoomTest123!"));
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        adminId = await InsertUserAsync(RoleCodes.Admin, "Admin Phòng Test", "0961000001");
        dentist1Id = await InsertUserAsync(RoleCodes.Dentist, "BS. Phòng 1", "0961000002");
        dentist2Id = await InsertUserAsync(RoleCodes.Dentist, "BS. Phòng 2", "0961000003");
        assistant1Id = await InsertUserAsync(RoleCodes.Assistant, "PT. Hỗ trợ 1", "0961000004");
        assistant2Id = await InsertUserAsync(RoleCodes.Assistant, "PT. Hỗ trợ 2", "0961000005");

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO "Patients" ("FullName", "Phone", "DateOfBirth", "CitizenIdVersion", "IsActive", "CreatedAt")
                VALUES
                    ('Bệnh nhân Ca Đang Khám', '0961000011', '1990-01-01', 0, TRUE, NOW()),
                    ('Bệnh nhân Lịch Sử', '0961000012', '1985-05-10', 0, TRUE, NOW())
                RETURNING "PatientId";
                """;
            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            patient1Id = reader.GetInt32(0);
            await reader.ReadAsync();
            patient2Id = reader.GetInt32(0);
        }

        async Task<int> InsertLegacyChairAsync(string number, string roomName, int status, bool isActive)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO "DentalChairs" (
                    "ChairNumber", "RoomName", "Status", "Description", "IsActive",
                    "CreatedByUserId", "Version", "CreatedAt"
                )
                VALUES (@num, @room, @status, 'Ghế lịch sử trước E1', @active, @actor, 2, NOW() - INTERVAL '5 days')
                RETURNING "Id";
                """;
            cmd.Parameters.AddWithValue("num", number);
            cmd.Parameters.AddWithValue("room", roomName);
            cmd.Parameters.AddWithValue("status", status);
            cmd.Parameters.AddWithValue("active", isActive);
            cmd.Parameters.AddWithValue("actor", adminId);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        // 2. Tạo xung đột RoomName trùng ("Phòng Khám 1" và "  phòng   khám 1 ") ở schema cũ và kiểm chứng migration chặn trước ghi
        chair1Id = await InsertLegacyChairAsync("G01", "Phòng Khám 1", (int)DentalChairStatus.Occupied, true);
        chair2Id = await InsertLegacyChairAsync("G02", "  phòng   khám 1 ", (int)DentalChairStatus.Available, true);
        chair3Id = await InsertLegacyChairAsync("G03", "Phòng Tiểu Phẫu", (int)DentalChairStatus.Available, true);

        var duplicateBlocked = false;
        try
        {
            await migrator.MigrateAsync(RoomMigration);
        }
        catch (PostgresException ex) when (ex.MessageText.Contains("nhiều ghế trùng tên phòng", StringComparison.Ordinal))
        {
            duplicateBlocked = true;
        }
        check(duplicateBlocked, "EF migration preflight blocks duplicate normalized RoomName before writing ClinicRooms");
        check(
            !(await setup.Database.GetAppliedMigrationsAsync()).Contains(RoomMigration),
            "Failed duplicate-RoomName migration rolls back DDL and keeps DB at pre-E1 version");

        // 3. Sửa tên phòng G02 nhưng tạo xung đột ca InProgress gắn với ghế Maintenance (Status=3)
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE "DentalChairs" SET "RoomName" = 'Phòng Khám 2' WHERE "Id" = @chair2;
                UPDATE "DentalChairs" SET "Status" = 3 WHERE "Id" = @chair1;
                INSERT INTO "Visits" (
                    "PatientId", "Status", "StartedAt", "DentistId", "ChairId",
                    "CreatedByUserId", "Diagnosis", "IsLocked", "CreatedAt"
                )
                VALUES (@p1, 'InProgress', NOW() - INTERVAL '30 minutes', @d1, @chair1, @actor, 'Đang điều trị tủy', FALSE, NOW() - INTERVAL '30 minutes')
                RETURNING "VisitId";
                """;
            cmd.Parameters.AddWithValue("chair1", chair1Id);
            cmd.Parameters.AddWithValue("chair2", chair2Id);
            cmd.Parameters.AddWithValue("p1", patient1Id);
            cmd.Parameters.AddWithValue("d1", dentist1Id);
            cmd.Parameters.AddWithValue("actor", adminId);
            activeVisitId = (int)(await cmd.ExecuteScalarAsync())!;
        }

        var activeConflictBlocked = false;
        try
        {
            await migrator.MigrateAsync(RoomMigration);
        }
        catch (PostgresException ex) when (ex.MessageText.Contains("ca đang khám gắn với ghế bảo trì", StringComparison.Ordinal))
        {
            activeConflictBlocked = true;
        }
        check(activeConflictBlocked, "EF migration preflight blocks InProgress visit on Maintenance chair before writing");

        // 4. Đưa ghế G01 về trạng thái Occupied hợp lệ và thêm dữ liệu StaffWorkspaces + Visits lịch sử
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE "DentalChairs" SET "Status" = 2 WHERE "Id" = @chair1;
                INSERT INTO "StaffWorkspaces" ("UserId", "ChairId", "DentistId", "Version", "UpdatedByUserId", "CreatedAt")
                VALUES
                    (@d1, @chair1, NULL, 3, @actor, NOW() - INTERVAL '1 day'),
                    (@d2, @chair2, NULL, 1, @actor, NOW() - INTERVAL '1 day'),
                    (@a1, NULL, @d1, 2, @actor, NOW() - INTERVAL '1 day'),
                    (@a2, NULL, @d1, 1, @actor, NOW() - INTERVAL '1 day');
                """;
            cmd.Parameters.AddWithValue("chair1", chair1Id);
            cmd.Parameters.AddWithValue("chair2", chair2Id);
            cmd.Parameters.AddWithValue("d1", dentist1Id);
            cmd.Parameters.AddWithValue("d2", dentist2Id);
            cmd.Parameters.AddWithValue("a1", assistant1Id);
            cmd.Parameters.AddWithValue("a2", assistant2Id);
            cmd.Parameters.AddWithValue("actor", adminId);
            await cmd.ExecuteNonQueryAsync();
        }

        async Task<int> InsertHistoricalVisitAsync(int patientId, int dentistId, int chairId, string status, bool isLocked)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO "Visits" (
                    "PatientId", "Status", "StartedAt", "EndedAt", "DentistId", "ChairId",
                    "CreatedByUserId", "Diagnosis", "IsLocked", "CreatedAt"
                )
                VALUES (
                    @p, @status, NOW() - INTERVAL '3 days', NOW() - INTERVAL '2 days',
                    @d, @c, @actor, 'Lịch sử khám trước E1', @locked, NOW() - INTERVAL '3 days'
                )
                RETURNING "VisitId";
                """;
            cmd.Parameters.AddWithValue("p", patientId);
            cmd.Parameters.AddWithValue("status", status);
            cmd.Parameters.AddWithValue("d", dentistId);
            cmd.Parameters.AddWithValue("c", chairId);
            cmd.Parameters.AddWithValue("actor", adminId);
            cmd.Parameters.AddWithValue("locked", isLocked);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        completedVisit1Id = await InsertHistoricalVisitAsync(patient1Id, dentist1Id, chair1Id, VisitStatuses.Completed, true);
        completedVisit2Id = await InsertHistoricalVisitAsync(patient2Id, dentist2Id, chair2Id, VisitStatuses.Completed, true);
        cancelledVisitId = await InsertHistoricalVisitAsync(patient2Id, dentist1Id, chair3Id, VisitStatuses.Cancelled, false);

        // 5. Nâng cấp DB từ schema cũ lên AddIndependentClinicRooms thành công
        await migrator.MigrateAsync(RoomMigration);
        var appliedAfter = (await setup.Database.GetAppliedMigrationsAsync()).ToList();
        check(appliedAfter.Contains(RoomMigration), "Isolated test DB upgraded to AddIndependentClinicRooms successfully");

        await using var db = new DentalDbContext(options);
        var rooms = await db.ClinicRooms.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        var chairsAfter = await db.DentalChairs.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        var workspacesAfter = await db.StaffWorkspaces.AsNoTracking().OrderBy(w => w.UserId).ToListAsync();
        var visitsAfter = await db.Visits.AsNoTracking().OrderBy(v => v.VisitId).ToListAsync();

        check(rooms.Count == 3, "Three independent ClinicRooms created from three non-conflicting legacy chairs");
        check(
            chairsAfter.Count == 3 &&
            chairsAfter.All(c => c.RoomId.HasValue && rooms.Any(r => r.Id == c.RoomId.Value && r.LegacyChairId == c.Id)),
            "All legacy DentalChairs preserve Id/ChairNumber/RoomName/Status/Version and link to their ClinicRoom");

        var room1 = rooms.Single(r => r.LegacyChairId == chair1Id);
        var room2 = rooms.Single(r => r.LegacyChairId == chair2Id);
        var room3 = rooms.Single(r => r.LegacyChairId == chair3Id);

        check(
            room1.RoomCode == "G01" && room1.RoomName == "Phòng Khám 1" && room1.Status == DentalChairStatus.Occupied && room1.Version == 2 &&
            room2.RoomCode == "G02" && room2.RoomName == "Phòng Khám 2" && room2.Status == DentalChairStatus.Available &&
            room3.RoomCode == "G03" && room3.RoomName == "Phòng Tiểu Phẫu" && room3.Status == DentalChairStatus.Available,
            "ClinicRooms preserve exact status, version, code and normalized display name from legacy chairs");

        check(
            workspacesAfter.Single(w => w.UserId == dentist1Id).ChairId == chair1Id &&
            workspacesAfter.Single(w => w.UserId == dentist1Id).RoomId == room1.Id &&
            workspacesAfter.Single(w => w.UserId == dentist1Id).Version == 3 &&
            workspacesAfter.Single(w => w.UserId == dentist2Id).ChairId == chair2Id &&
            workspacesAfter.Single(w => w.UserId == dentist2Id).RoomId == room2.Id &&
            workspacesAfter.Where(w => w.UserId == assistant1Id || w.UserId == assistant2Id)
                .All(w => w.DentistId == dentist1Id && w.ChairId == null && w.RoomId == null),
            "Dentist workspaces link RoomId while keeping ChairId/Version; multiple assistants keep only DentistId link");

        check(
            visitsAfter.Count == 4 &&
            visitsAfter.Single(v => v.VisitId == activeVisitId) is { Status: VisitStatuses.InProgress, IsLocked: false } vActive &&
            vActive.ChairId == chair1Id && vActive.RoomId == room1.Id &&
            visitsAfter.Single(v => v.VisitId == completedVisit1Id) is { Status: VisitStatuses.Completed, IsLocked: true } vComp1 &&
            vComp1.ChairId == chair1Id && vComp1.RoomId == room1.Id &&
            visitsAfter.Single(v => v.VisitId == completedVisit2Id) is { Status: VisitStatuses.Completed, IsLocked: true } vComp2 &&
            vComp2.ChairId == chair2Id && vComp2.RoomId == room2.Id &&
            visitsAfter.Single(v => v.VisitId == cancelledVisitId) is { Status: VisitStatuses.Cancelled, IsLocked: false } vCanc &&
            vCanc.ChairId == chair3Id && vCanc.RoomId == room3.Id,
            "All active and historical Visits retain ChairId/Status/Lock state and gain accurate RoomId links without data loss");

        // 6. Kiểm chứng RoomDataMigrationRunner chặn xung đột khi phát sinh ghế chưa liên kết có RoomName trùng
        var conflictChair = new DentalChair
        {
            ChairNumber = "G-CONFLICT",
            RoomName = "  phòng   khám 1  ",
            Status = DentalChairStatus.Available,
            IsActive = true,
            CreatedByUserId = adminId,
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.DentalChairs.Add(conflictChair);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var conflictReport = await RoomDataMigrationRunner.InspectPreflightAsync(db);
        check(
            !conflictReport.CanMigrateSafely &&
            conflictReport.DuplicateRoomNameGroups.Count == 1 &&
            conflictReport.DuplicateRoomNameGroups[0].NormalizedRoomName == "PHÒNG KHÁM 1",
            "RoomDataMigrationRunner.InspectPreflightAsync detects duplicate RoomName and marks CanMigrateSafely=false");

        var runnerBlocked = false;
        try
        {
            await RoomDataMigrationRunner.MigrateRoomsAsync(db, adminId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("DuplicateRoomNames=1", StringComparison.Ordinal))
        {
            runnerBlocked = true;
        }
        check(
            runnerBlocked &&
            await db.ClinicRooms.CountAsync() == 3 &&
            !await db.AuditLogs.AnyAsync(a => a.Action == RoomDataMigrationRunner.MigrationAuditAction),
            "RoomDataMigrationRunner blocks before writing when duplicate RoomName exists");

        // 7. Đổi conflictChair thành phòng hợp lệ mới ("Phòng Điều Trị 2") + gắn Visit lịch sử, kiểm chứng Preview (dry-run) và MigrateRoomsAsync
        var trackedChair4 = await db.DentalChairs.SingleAsync(c => c.Id == conflictChair.Id);
        trackedChair4.ChairNumber = "G04";
        trackedChair4.RoomName = "  Phòng   Điều Trị   2 ";
        var visitOnG04 = new Visit
        {
            PatientId = patient2Id,
            DentistId = dentist2Id,
            ChairId = trackedChair4.Id,
            Status = VisitStatuses.Completed,
            IsLocked = true,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            EndedAt = DateTime.UtcNow.AddHours(-1),
            CreatedByUserId = adminId,
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        db.Visits.Add(visitOnG04);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var dryRunResult = await RoomDataMigrationRunner.PreviewAsync(db, adminId);
        check(
            dryRunResult.DryRun &&
            dryRunResult.Preflight.CanMigrateSafely &&
            dryRunResult.CreatedRooms == 1 &&
            dryRunResult.LinkedChairs == 1 &&
            dryRunResult.LinkedVisits == 1 &&
            await db.ClinicRooms.CountAsync() == 3 &&
            (await db.DentalChairs.AsNoTracking().SingleAsync(c => c.Id == trackedChair4.Id)).RoomId == null &&
            !await db.AuditLogs.AnyAsync(a => a.Action == RoomDataMigrationRunner.MigrationAuditAction),
            "PreviewAsync (dry-run) computes exact migration plan and rolls back without persisting rooms or audit");

        var commitResult = await RoomDataMigrationRunner.MigrateRoomsAsync(db, adminId, dryRun: false);
        var room4 = await db.ClinicRooms.AsNoTracking().SingleOrDefaultAsync(r => r.LegacyChairId == trackedChair4.Id);
        var auditLog = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.Action == RoomDataMigrationRunner.MigrationAuditAction);
        check(
            !commitResult.DryRun &&
            commitResult.CreatedRooms == 1 &&
            commitResult.LinkedChairs == 1 &&
            commitResult.LinkedVisits == 1 &&
            room4 is { RoomCode: "G04", RoomName: "Phòng Điều Trị 2", NormalizedRoomName: "PHÒNG ĐIỀU TRỊ 2" } &&
            (await db.DentalChairs.AsNoTracking().SingleAsync(c => c.Id == trackedChair4.Id)).RoomId == room4.Id &&
            (await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == visitOnG04.VisitId)).RoomId == room4.Id &&
            auditLog is not null &&
            auditLog.Detail!.Contains("DL-361/DL-367", StringComparison.Ordinal) &&
            !auditLog.Detail.Contains("0961", StringComparison.Ordinal),
            "MigrateRoomsAsync commits new ClinicRoom, links ChairId/Visit.RoomId, and records PII-free audit log");

        var rerunResult = await RoomDataMigrationRunner.MigrateRoomsAsync(db, adminId, dryRun: false);
        check(
            rerunResult.CreatedRooms == 0 &&
            rerunResult.LinkedChairs == 0 &&
            rerunResult.LinkedWorkspaces == 0 &&
            rerunResult.LinkedVisits == 0 &&
            await db.AuditLogs.CountAsync(a => a.Action == RoomDataMigrationRunner.MigrationAuditAction) == 1,
            "Re-running MigrateRoomsAsync is idempotent and writes no duplicate audit");
    }
}
