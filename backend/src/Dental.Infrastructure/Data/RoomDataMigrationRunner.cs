using System.Data;
using System.Text.Json;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data;

/// <summary>
/// Bộ chạy tách dữ liệu phòng độc lập từ danh mục ghế lịch sử (DL-361, DL-367).
/// Bắt buộc chạy preflight chặn xung đột RoomName/Visit trước khi ghi; giữ nguyên ChairId lịch sử trên DentalChairs, StaffWorkspaces và Visits.
/// </summary>
public static class RoomDataMigrationRunner
{
    public const string MigrationAuditAction = "MOD_MST_ROOM_DATA_MIGRATED";

    public sealed record RoomDataMigrationResult(
        RoomMigrationPreflight.RoomMigrationPreflightReport Preflight,
        int CreatedRooms,
        int LinkedChairs,
        int LinkedWorkspaces,
        int LinkedVisits,
        int PreservedActiveVisits,
        int PreservedHistoricalVisits,
        bool DryRun);

    public static void AssertNotProductionDatabase(DentalDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        var dbName = db.Database.GetDbConnection().Database?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(dbName) ||
            string.Equals(dbName, "dental_db", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dbName, "dental_clinic", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Từ chối chạy chuyển đổi dữ liệu phòng trực tiếp trên cơ sở dữ liệu chính; chỉ cho phép chạy trên bản sao hoặc DB kiểm thử cô lập.");
        }
    }

    public static async Task<RoomMigrationPreflight.RoomMigrationPreflightReport> InspectPreflightAsync(
        DentalDbContext db,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var chairs = await db.DentalChairs
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new RoomMigrationPreflight.ChairRoomSnapshot(
                c.Id,
                c.ChairNumber,
                c.RoomName,
                c.Status,
                c.IsActive,
                c.RoomId))
            .ToListAsync(ct);

        var workspaces = await db.StaffWorkspaces
            .AsNoTracking()
            .OrderBy(w => w.UserId)
            .Select(w => new RoomMigrationPreflight.WorkspaceRoomSnapshot(
                w.UserId,
                w.User.Role != null ? w.User.Role.RoleCode : null,
                w.User.IsActive,
                w.ChairId,
                w.DentistId,
                w.RoomId))
            .ToListAsync(ct);

        var visits = await db.Visits
            .AsNoTracking()
            .Where(v => v.ChairId != null || v.RoomId != null || v.Status == VisitStatuses.InProgress)
            .OrderBy(v => v.VisitId)
            .Select(v => new RoomMigrationPreflight.VisitRoomSnapshot(
                v.VisitId,
                v.Status,
                v.DentistId,
                v.ChairId,
                v.RoomId))
            .ToListAsync(ct);

        return RoomMigrationPreflight.Analyze(chairs, workspaces, visits);
    }

    public static Task<RoomDataMigrationResult> PreviewAsync(
        DentalDbContext db,
        int actorUserId,
        CancellationToken ct = default)
        => MigrateRoomsAsync(db, actorUserId, dryRun: true, ct);

    public static async Task<RoomDataMigrationResult> MigrateRoomsAsync(
        DentalDbContext db,
        int actorUserId,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        AssertNotProductionDatabase(db);
        if (actorUserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(actorUserId), "ActorUserId phải lớn hơn 0.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var preflight = await InspectPreflightAsync(db, ct);
        if (!preflight.CanMigrateSafely)
        {
            await tx.RollbackAsync(ct);
            throw new InvalidOperationException(
                $"Preflight dữ liệu phòng thất bại (EmptyRoomName={preflight.EmptyRoomNameChairIds.Count}, " +
                $"DuplicateRoomNames={preflight.DuplicateRoomNameGroups.Count}, " +
                $"ConflictingActiveVisits={preflight.ConflictingActiveVisitIds.Count}, " +
                $"ConflictingWorkspaces={preflight.ConflictingWorkspaceUserIds.Count}). " +
                "Không tự chọn hoặc gộp phòng khi có xung đột.");
        }

        var chairs = await db.DentalChairs.OrderBy(c => c.Id).ToListAsync(ct);
        var existingRooms = await db.ClinicRooms.OrderBy(r => r.Id).ToListAsync(ct);
        var roomByChairId = new Dictionary<int, ClinicRoom>();
        var createdRooms = 0;
        var linkedChairs = 0;

        foreach (var chair in chairs)
        {
            var normalizedName = RoomMigrationPreflight.NormalizeRoomName(chair.RoomName);
            var cleanName = RoomMigrationPreflight.CleanDisplayRoomName(chair.RoomName);
            var normalizedCode = chair.ChairNumber.Trim().ToUpperInvariant();

            var room = existingRooms.FirstOrDefault(r =>
                r.LegacyChairId == chair.Id ||
                (chair.RoomId.HasValue && r.Id == chair.RoomId.Value) ||
                string.Equals(r.NormalizedRoomName, normalizedName, StringComparison.Ordinal));

            if (room is not null)
            {
                if ((room.LegacyChairId.HasValue && room.LegacyChairId.Value != chair.Id) ||
                    !string.Equals(room.NormalizedRoomName, normalizedName, StringComparison.Ordinal))
                {
                    await tx.RollbackAsync(ct);
                    throw new InvalidOperationException(
                        $"Phát hiện xung đột giữa phòng độc lập hiện có (RoomId={room.Id}) và ghế lịch sử (ChairId={chair.Id}).");
                }

                if (!room.LegacyChairId.HasValue)
                    room.LegacyChairId = chair.Id;
            }
            else
            {
                if (existingRooms.Any(r => string.Equals(r.RoomCode, normalizedCode, StringComparison.OrdinalIgnoreCase)))
                {
                    await tx.RollbackAsync(ct);
                    throw new InvalidOperationException(
                        $"Mã phòng '{normalizedCode}' đã tồn tại trên một phòng khác; từ chối tự đổi mã khi chuyển đổi.");
                }

                room = new ClinicRoom
                {
                    RoomCode = normalizedCode,
                    RoomName = cleanName,
                    NormalizedRoomName = normalizedName,
                    Status = chair.Status,
                    Description = chair.Description,
                    IsActive = chair.IsActive,
                    LegacyChairId = chair.Id,
                    CreatedByUserId = chair.CreatedByUserId > 0 ? chair.CreatedByUserId : actorUserId,
                    UpdatedByUserId = chair.UpdatedByUserId,
                    Version = chair.Version,
                    CreatedAt = chair.CreatedAt,
                    UpdatedAt = chair.UpdatedAt
                };
                db.ClinicRooms.Add(room);
                existingRooms.Add(room);
                createdRooms++;
            }

            roomByChairId[chair.Id] = room;
        }

        if (createdRooms > 0)
            await db.SaveChangesAsync(ct);

        foreach (var chair in chairs)
        {
            var room = roomByChairId[chair.Id];
            if (chair.RoomId != room.Id)
            {
                chair.RoomId = room.Id;
                linkedChairs++;
            }
        }

        var workspaces = await db.StaffWorkspaces
            .Where(w => w.ChairId != null)
            .OrderBy(w => w.UserId)
            .ToListAsync(ct);

        var linkedWorkspaces = 0;
        foreach (var ws in workspaces)
        {
            var targetRoom = roomByChairId[ws.ChairId!.Value];
            if (ws.RoomId != targetRoom.Id)
            {
                ws.RoomId = targetRoom.Id;
                linkedWorkspaces++;
            }
        }

        var visits = await db.Visits
            .Where(v => v.ChairId != null)
            .OrderBy(v => v.VisitId)
            .ToListAsync(ct);

        var linkedVisits = 0;
        var preservedActiveVisits = 0;
        var preservedHistoricalVisits = 0;
        foreach (var visit in visits)
        {
            if (string.Equals(visit.Status, VisitStatuses.InProgress, StringComparison.Ordinal))
                preservedActiveVisits++;
            else
                preservedHistoricalVisits++;

            var targetRoom = roomByChairId[visit.ChairId!.Value];
            if (visit.RoomId != targetRoom.Id)
            {
                visit.RoomId = targetRoom.Id;
                linkedVisits++;
            }
        }

        var hasChanges = createdRooms > 0 || linkedChairs > 0 || linkedWorkspaces > 0 || linkedVisits > 0;
        if (hasChanges)
        {
            db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = MigrationAuditAction,
                EntityType = "ClinicRoom",
                EntityId = null,
                Detail = JsonSerializer.Serialize(new
                {
                    createdRooms,
                    linkedChairs,
                    linkedWorkspaces,
                    linkedVisits,
                    preservedActiveVisits,
                    preservedHistoricalVisits,
                    decision = "DL-361/DL-367"
                }),
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);
        }

        if (dryRun)
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
        }
        else
        {
            await tx.CommitAsync(ct);
        }

        return new RoomDataMigrationResult(
            Preflight: preflight,
            CreatedRooms: createdRooms,
            LinkedChairs: linkedChairs,
            LinkedWorkspaces: linkedWorkspaces,
            LinkedVisits: linkedVisits,
            PreservedActiveVisits: preservedActiveVisits,
            PreservedHistoricalVisits: preservedHistoricalVisits,
            DryRun: dryRun);
    }
}
