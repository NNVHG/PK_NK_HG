using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class StaffWorkspaceRepository(DentalDbContext db) : IStaffWorkspaceRepository
{
    public async Task<StaffWorkspace?> FindAsync(int userId, CancellationToken ct = default)
        => await db.Users.AsNoTracking()
            .Where(x => x.UserId == userId &&
                (x.WorkspaceVersion > 0 || x.WorkspaceRoomId.HasValue || x.WorkspaceDentistId.HasValue || x.WorkspaceDate.HasValue))
            .Select(x => new StaffWorkspace
            {
                UserId = x.UserId,
                RoomId = x.WorkspaceRoomId,
                WorkDate = x.WorkspaceDate,
                DentistId = x.WorkspaceDentistId,
                Version = x.WorkspaceVersion,
                UpdatedByUserId = x.WorkspaceUpdatedByUserId ?? x.UserId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.WorkspaceUpdatedAt,
            })
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<StaffWorkspace>> GetAllAsync(CancellationToken ct = default)
        => await db.Users.AsNoTracking()
            .Where(x => x.IsActive && (x.Role.RoleCode == RoleCodes.Dentist || x.Role.RoleCode == RoleCodes.Assistant) &&
                (x.WorkspaceVersion > 0 || x.WorkspaceRoomId.HasValue || x.WorkspaceDentistId.HasValue || x.WorkspaceDate.HasValue))
            .OrderBy(x => x.UserId)
            .Select(x => new StaffWorkspace
            {
                UserId = x.UserId,
                RoomId = x.WorkspaceRoomId,
                WorkDate = x.WorkspaceDate,
                DentistId = x.WorkspaceDentistId,
                Version = x.WorkspaceVersion,
                UpdatedByUserId = x.WorkspaceUpdatedByUserId ?? x.UserId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.WorkspaceUpdatedAt,
            })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StaffWorkspace>> GetAssistantLinksForUpdateAsync(int dentistId, CancellationToken ct = default)
        => await db.Users.AsNoTracking()
            .Where(x => x.WorkspaceDentistId == dentistId)
            .Select(x => new StaffWorkspace
            {
                UserId = x.UserId,
                RoomId = x.WorkspaceRoomId,
                WorkDate = x.WorkspaceDate,
                DentistId = x.WorkspaceDentistId,
                Version = x.WorkspaceVersion,
                UpdatedByUserId = x.WorkspaceUpdatedByUserId ?? x.UserId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.WorkspaceUpdatedAt,
            })
            .ToListAsync(ct);

    public Task<bool> ChairClaimedAsync(int chairId, int exceptUserId, CancellationToken ct = default)
        => ClaimsForRoom(chairId, exceptUserId).AnyAsync(ct);

    public async Task<bool> ReleaseStaleChairClaimsAsync(
        int roomId,
        int exceptUserId,
        DateOnly workDate,
        int actorId,
        DateTime updatedAtUtc,
        CancellationToken ct = default)
    {
        var stale = await ClaimsForRoom(roomId, exceptUserId)
            .Where(x => x.WorkspaceDate == null || x.WorkspaceDate != workDate ||
                !x.IsActive || x.Role.RoleCode != RoleCodes.Dentist)
            .ToListAsync(ct);
        if (stale.Count == 0)
            return false;

        foreach (var item in stale)
        {
            item.WorkspaceRoomId = null;
            item.WorkspaceDate = null;
            item.WorkspaceVersion++;
            item.WorkspaceUpdatedAt = updatedAtUtc;
            item.WorkspaceUpdatedByUserId = actorId;
        }
        return true;
    }

    public Task AddAsync(StaffWorkspace workspace, CancellationToken ct = default)
        => UpsertAsync(workspace, 0, ct);

    public async Task UpsertAsync(StaffWorkspace workspace, int expectedVersion, CancellationToken ct = default)
    {
        var user = await db.Users.FirstAsync(x => x.UserId == workspace.UserId, ct);
        db.Entry(user).Property(x => x.WorkspaceVersion).OriginalValue = expectedVersion;
        user.WorkspaceRoomId = workspace.RoomId;
        user.WorkspaceDentistId = workspace.DentistId;
        user.WorkspaceDate = workspace.WorkDate;
        user.WorkspaceVersion = workspace.Version;
        user.WorkspaceUpdatedAt = workspace.UpdatedAt;
        user.WorkspaceUpdatedByUserId = workspace.UpdatedByUserId;
    }

    private IQueryable<User> ClaimsForRoom(int roomId, int exceptUserId)
        => db.Users.Where(u => u.UserId != exceptUserId && u.WorkspaceRoomId == roomId);
}
