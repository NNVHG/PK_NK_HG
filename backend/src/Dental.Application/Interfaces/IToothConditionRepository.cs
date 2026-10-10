using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IToothConditionRepository
{
    Task<IReadOnlyList<ToothCondition>> GetForVisitAsync(int visitId, CancellationToken ct = default);
    Task AddAsync(ToothCondition condition, CancellationToken ct = default);
}
