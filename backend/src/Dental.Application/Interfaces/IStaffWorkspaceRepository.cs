using Dental.Domain.Entities;
namespace Dental.Application.Interfaces;
public interface IStaffWorkspaceRepository
{
    Task<StaffWorkspace?> FindAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<StaffWorkspace>> GetAllAsync(CancellationToken ct = default);
    Task<bool> ChairClaimedAsync(int chairId, int exceptUserId, CancellationToken ct = default);
    Task AddAsync(StaffWorkspace workspace, CancellationToken ct = default);
}
