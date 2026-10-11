using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IVisitServiceRepository
{
    Task<IReadOnlyList<VisitService>> GetForVisitAsync(int visitId, CancellationToken ct = default);
    Task AddRangeAsync(IReadOnlyList<VisitService> rows, CancellationToken ct = default);
}
