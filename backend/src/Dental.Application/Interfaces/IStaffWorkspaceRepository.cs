using Dental.Domain.Entities;
namespace Dental.Application.Interfaces;
public interface IStaffWorkspaceRepository
{
    Task<StaffWorkspace?> FindAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<StaffWorkspace>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StaffWorkspace>> GetAssistantLinksForUpdateAsync(int dentistId, CancellationToken ct = default);
    Task<bool> ChairClaimedAsync(int roomId, int exceptUserId, CancellationToken ct = default);
    Task<bool> ReleaseStaleChairClaimsAsync(int roomId, int exceptUserId, DateOnly workDate, int actorId, DateTime updatedAtUtc, CancellationToken ct = default);
    Task AddAsync(StaffWorkspace workspace, CancellationToken ct = default);
    Task UpsertAsync(StaffWorkspace workspace, int expectedVersion, CancellationToken ct = default);
}
