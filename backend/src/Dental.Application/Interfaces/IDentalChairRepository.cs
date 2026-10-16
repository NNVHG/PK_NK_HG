using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IDentalChairRepository
{
    Task<IReadOnlyList<DentalChair>> GetAllAsync(CancellationToken ct = default);
    Task<bool> IsAssignedAsync(int chairId, CancellationToken ct = default);
    Task<DentalChair?> FindAsync(int id, CancellationToken ct = default);
    Task<bool> NumberExistsAsync(string normalizedNumber, CancellationToken ct = default);
    Task AddAsync(DentalChair chair, CancellationToken ct = default);
    Task<PagedResult<DentalChair>> GetPageAsync(DentalChairQuery query, CancellationToken ct = default);
}
