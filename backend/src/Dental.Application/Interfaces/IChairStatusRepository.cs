using Dental.Application.Common;
using Dental.Application.Features.MOD_ASS.DTOs;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IChairStatusRepository
{
    Task<bool> ActorAllowedAsync(int actorId, string role, CancellationToken ct = default);
    Task<PagedResult<ChairStatusResponse>> GetPageAsync(ChairStatusQuery query, CancellationToken ct = default);
    Task<DentalChair?> FindAsync(int id, CancellationToken ct = default);
    Task<bool> IsAssignedAsync(int id, CancellationToken ct = default);
    Task<bool> HasActiveVisitAsync(int id, CancellationToken ct = default);
}
