using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class StaffWorkspaceRepository(DentalDbContext db) : IStaffWorkspaceRepository
{
    public Task<StaffWorkspace?> FindAsync(int userId, CancellationToken ct = default)
        => db.StaffWorkspaces.FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task<IReadOnlyList<StaffWorkspace>> GetAllAsync(CancellationToken ct = default)
        => await db.StaffWorkspaces.AsNoTracking()
            .Where(x => x.User.IsActive && (x.User.Role.RoleCode == RoleCodes.Dentist || x.User.Role.RoleCode == RoleCodes.Assistant))
            .OrderBy(x => x.UserId).ToListAsync(ct);

    public async Task<IReadOnlyList<StaffWorkspace>> GetAssistantLinksForUpdateAsync(int dentistId, CancellationToken ct = default)
        => await db.StaffWorkspaces.Where(x => x.DentistId == dentistId).ToListAsync(ct);

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
            .Where(x => x.WorkDate == null || x.WorkDate != workDate ||
                !x.User.IsActive || x.User.Role.RoleCode != RoleCodes.Dentist)
            .ToListAsync(ct);
        if (stale.Count == 0)
            return false;

        foreach (var item in stale)
        {
            item.RoomId = null;
            item.WorkDate = null;
            item.Version++;
            item.UpdatedAt = updatedAtUtc;
            item.UpdatedByUserId = actorId;
        }
        return true;
    }

    public async Task AddAsync(StaffWorkspace workspace, CancellationToken ct = default)
        => await db.StaffWorkspaces.AddAsync(workspace, ct);

    private IQueryable<StaffWorkspace> ClaimsForRoom(int roomId, int exceptUserId)
        => db.StaffWorkspaces.Where(workspace => workspace.UserId != exceptUserId && workspace.RoomId == roomId);
}
