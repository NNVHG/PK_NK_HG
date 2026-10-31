using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class StaffWorkspaceRepository(DentalDbContext db) : IStaffWorkspaceRepository
{
    public Task<StaffWorkspace?> FindAsync(int userId, CancellationToken ct = default)
        => db.StaffWorkspaces.FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task<IReadOnlyList<StaffWorkspace>> GetAllAsync(CancellationToken ct = default)
        => await db.StaffWorkspaces.AsNoTracking().OrderBy(x => x.UserId).ToListAsync(ct);

    public Task<bool> ChairClaimedAsync(int chairId, int exceptUserId, CancellationToken ct = default)
        => db.StaffWorkspaces.AnyAsync(x => x.ChairId == chairId && x.UserId != exceptUserId, ct);

    public async Task<bool> ReleaseStaleChairClaimsAsync(
        int chairId,
        int exceptUserId,
        DateOnly workDate,
        int actorId,
        DateTime updatedAtUtc,
        CancellationToken ct = default)
    {
        var stale = await db.StaffWorkspaces
            .Where(x => x.ChairId == chairId && x.UserId != exceptUserId && x.WorkDate != workDate)
            .ToListAsync(ct);
        if (stale.Count == 0)
            return false;

        foreach (var item in stale)
        {
            item.ChairId = null;
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
}
