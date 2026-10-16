using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Repositories;
public sealed class StaffWorkspaceRepository(DentalDbContext db) : IStaffWorkspaceRepository
{
    public Task<StaffWorkspace?> FindAsync(int userId, CancellationToken ct = default) => db.StaffWorkspaces.FirstOrDefaultAsync(x => x.UserId == userId, ct);
    public async Task<IReadOnlyList<StaffWorkspace>> GetAllAsync(CancellationToken ct = default) => await db.StaffWorkspaces.AsNoTracking().OrderBy(x => x.UserId).ToListAsync(ct);
    public Task<bool> ChairClaimedAsync(int chairId, int exceptUserId, CancellationToken ct = default) => db.StaffWorkspaces.AnyAsync(x => x.ChairId == chairId && x.UserId != exceptUserId, ct);
    public Task<bool> DentistPairedAsync(int dentistId, int exceptUserId, CancellationToken ct = default) => db.StaffWorkspaces.AnyAsync(x => x.DentistId == dentistId && x.UserId != exceptUserId, ct);
    public async Task AddAsync(StaffWorkspace workspace, CancellationToken ct = default) => await db.StaffWorkspaces.AddAsync(workspace, ct);
}
