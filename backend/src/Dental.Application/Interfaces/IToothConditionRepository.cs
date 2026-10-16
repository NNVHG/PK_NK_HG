using Dental.Domain.Entities;
using Dental.Application.Features.MOD_FDI.DTOs;

namespace Dental.Application.Interfaces;

public interface IToothConditionRepository
{
    Task<IReadOnlyList<ToothCondition>> GetForVisitAsync(int visitId, CancellationToken ct = default);
    Task<IReadOnlyList<LatestToothConditionResponse>> GetLatestForPatientAsync(int patientId, CancellationToken ct = default);
    Task<ToothCondition?> GetForUpdateAsync(int visitId, int conditionId, CancellationToken ct = default);
    Task AddAsync(ToothCondition condition, CancellationToken ct = default);
    void Remove(ToothCondition condition);
}
