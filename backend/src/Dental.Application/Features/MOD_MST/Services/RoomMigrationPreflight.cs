using System.Text.RegularExpressions;
using Dental.Domain.Constants;
using Dental.Domain.Enums;

namespace Dental.Application.Features.MOD_MST.Services;

/// <summary>
/// Preflight dữ liệu phòng/ghế và ca đang hoạt động trước khi tách phòng độc lập (DL-361, DL-367).
/// Chặn mọi xung đột RoomName hoặc ca InProgress bất nhất; không tự gộp hoặc chọn đại diện khi nhiều ghế trùng tên phòng.
/// </summary>
public static class RoomMigrationPreflight
{
    public sealed record ChairRoomSnapshot(
        int ChairId,
        string ChairNumber,
        string? RoomName,
        DentalChairStatus Status,
        bool IsActive,
        int? RoomId);

    public sealed record WorkspaceRoomSnapshot(
        int UserId,
        string? RoleCode,
        bool IsUserActive,
        int? ChairId,
        int? DentistId,
        int? RoomId);

    public sealed record VisitRoomSnapshot(
        int VisitId,
        string Status,
        int? DentistId,
        int? ChairId,
        int? RoomId);

    public sealed record DuplicateRoomNameGroup(
        string NormalizedRoomName,
        IReadOnlyList<int> ChairIds,
        IReadOnlyList<string> ChairNumbers);

    public sealed record RoomMigrationPreflightReport(
        int TotalChairs,
        int AlreadyLinkedChairs,
        int PendingChairsToMigrate,
        int ActiveInProgressVisits,
        int HistoricalVisitsWithChair,
        int ActiveDentistWorkspaces,
        IReadOnlyList<int> EmptyRoomNameChairIds,
        IReadOnlyList<DuplicateRoomNameGroup> DuplicateRoomNameGroups,
        IReadOnlyList<int> ConflictingActiveVisitIds,
        IReadOnlyList<int> ConflictingWorkspaceUserIds,
        bool CanMigrateSafely);

    public static string CleanDisplayRoomName(string? roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return string.Empty;
        return Regex.Replace(roomName.Trim(), @"\s+", " ");
    }

    public static string NormalizeRoomName(string? roomName)
        => CleanDisplayRoomName(roomName).ToUpperInvariant();

    public static RoomMigrationPreflightReport Analyze(
        IReadOnlyCollection<ChairRoomSnapshot> chairs,
        IReadOnlyCollection<WorkspaceRoomSnapshot> workspaces,
        IReadOnlyCollection<VisitRoomSnapshot> visits)
    {
        ArgumentNullException.ThrowIfNull(chairs);
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(visits);

        var emptyChairIds = chairs
            .Where(c => string.IsNullOrWhiteSpace(c.ChairNumber) || string.IsNullOrWhiteSpace(CleanDisplayRoomName(c.RoomName)))
            .Select(c => c.ChairId)
            .OrderBy(id => id)
            .ToArray();

        var validNameChairs = chairs
            .Where(c => !string.IsNullOrWhiteSpace(c.ChairNumber) && !string.IsNullOrWhiteSpace(CleanDisplayRoomName(c.RoomName)))
            .ToArray();

        var duplicateGroups = validNameChairs
            .GroupBy(c => NormalizeRoomName(c.RoomName), StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new DuplicateRoomNameGroup(
                g.Key,
                g.Select(x => x.ChairId).OrderBy(id => id).ToArray(),
                g.OrderBy(x => x.ChairId).Select(x => x.ChairNumber.Trim()).ToArray()))
            .ToArray();

        var duplicateChairIds = duplicateGroups
            .SelectMany(g => g.ChairIds)
            .ToHashSet();

        var chairById = chairs.ToDictionary(c => c.ChairId);

        // Kiểm tra xung đột trên ca đang khám (InProgress)
        var conflictingVisitIds = new HashSet<int>();
        var inProgressVisits = visits
            .Where(v => string.Equals(v.Status, VisitStatuses.InProgress, StringComparison.Ordinal))
            .ToArray();

        foreach (var visit in inProgressVisits)
        {
            if (visit.ChairId is int chairId)
            {
                if (!chairById.TryGetValue(chairId, out var chair) ||
                    !chair.IsActive ||
                    chair.Status is DentalChairStatus.Maintenance or DentalChairStatus.Inactive ||
                    duplicateChairIds.Contains(chairId) ||
                    (visit.RoomId.HasValue && chair.RoomId.HasValue && visit.RoomId.Value != chair.RoomId.Value))
                {
                    conflictingVisitIds.Add(visit.VisitId);
                }
            }
        }

        // Nhiều ca InProgress cùng giữ một ghế hoặc cùng tên phòng chuẩn hóa
        foreach (var group in inProgressVisits.Where(v => v.ChairId.HasValue).GroupBy(v => v.ChairId!.Value).Where(g => g.Count() > 1))
        {
            foreach (var v in group)
                conflictingVisitIds.Add(v.VisitId);
        }

        foreach (var group in inProgressVisits
            .Where(v => v.ChairId.HasValue && chairById.ContainsKey(v.ChairId.Value))
            .GroupBy(v => NormalizeRoomName(chairById[v.ChairId!.Value].RoomName), StringComparer.Ordinal)
            .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1))
        {
            foreach (var v in group)
                conflictingVisitIds.Add(v.VisitId);
        }

        // Kiểm tra xung đột trên vị trí làm việc (StaffWorkspaces)
        var conflictingWorkspaceUsers = new HashSet<int>();
        foreach (var ws in workspaces)
        {
            if (ws.ChairId.HasValue && ws.DentistId.HasValue)
            {
                conflictingWorkspaceUsers.Add(ws.UserId);
                continue;
            }

            if (ws.ChairId is int chairId)
            {
                if (!chairById.TryGetValue(chairId, out var chair) ||
                    duplicateChairIds.Contains(chairId) ||
                    (ws.RoomId.HasValue && chair.RoomId.HasValue && ws.RoomId.Value != chair.RoomId.Value))
                {
                    conflictingWorkspaceUsers.Add(ws.UserId);
                }
            }
        }

        foreach (var group in workspaces.Where(w => w.ChairId.HasValue).GroupBy(w => w.ChairId!.Value).Where(g => g.Count() > 1))
        {
            foreach (var ws in group)
                conflictingWorkspaceUsers.Add(ws.UserId);
        }

        foreach (var group in workspaces
            .Where(w => w.ChairId.HasValue && chairById.ContainsKey(w.ChairId.Value))
            .GroupBy(w => NormalizeRoomName(chairById[w.ChairId!.Value].RoomName), StringComparer.Ordinal)
            .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1))
        {
            foreach (var ws in group)
                conflictingWorkspaceUsers.Add(ws.UserId);
        }

        var alreadyLinked = chairs.Count(c => c.RoomId.HasValue);
        var pending = chairs.Count - alreadyLinked;
        var historicalVisitsWithChair = visits.Count(v =>
            v.ChairId.HasValue && !string.Equals(v.Status, VisitStatuses.InProgress, StringComparison.Ordinal));
        var activeDentistWorkspaces = workspaces.Count(w => w.ChairId.HasValue);

        var canMigrateSafely = emptyChairIds.Length == 0 &&
            duplicateGroups.Length == 0 &&
            conflictingVisitIds.Count == 0 &&
            conflictingWorkspaceUsers.Count == 0;

        return new RoomMigrationPreflightReport(
            TotalChairs: chairs.Count,
            AlreadyLinkedChairs: alreadyLinked,
            PendingChairsToMigrate: pending,
            ActiveInProgressVisits: inProgressVisits.Length,
            HistoricalVisitsWithChair: historicalVisitsWithChair,
            ActiveDentistWorkspaces: activeDentistWorkspaces,
            EmptyRoomNameChairIds: emptyChairIds,
            DuplicateRoomNameGroups: duplicateGroups,
            ConflictingActiveVisitIds: conflictingVisitIds.OrderBy(id => id).ToArray(),
            ConflictingWorkspaceUserIds: conflictingWorkspaceUsers.OrderBy(id => id).ToArray(),
            CanMigrateSafely: canMigrateSafely);
    }
}
