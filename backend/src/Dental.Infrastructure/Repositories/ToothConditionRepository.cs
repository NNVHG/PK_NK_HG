using Dental.Application.Interfaces;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class ToothConditionRepository(DentalDbContext db) : IToothConditionRepository
{
    public async Task<IReadOnlyList<ToothCondition>> GetForVisitAsync(int visitId, CancellationToken ct = default)
        => await db.Set<ToothCondition>().AsNoTracking().Where(x => x.VisitId == visitId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<LatestToothConditionResponse>> GetLatestForPatientAsync(int patientId, CancellationToken ct = default)
    {
        var patientConditions = db.Set<ToothCondition>().AsNoTracking()
            .Where(condition => condition.Visit.PatientId == patientId);
        return await patientConditions.Where(condition => !patientConditions.Any(other =>
                other.ToothNumber == condition.ToothNumber && other.Surface == condition.Surface &&
                (other.CreatedAt > condition.CreatedAt ||
                 other.CreatedAt == condition.CreatedAt && other.Id > condition.Id)))
            .OrderBy(condition => condition.ToothNumber)
            .ThenBy(condition => condition.Surface)
            .Select(condition => new LatestToothConditionResponse(condition.Id, condition.ToothNumber,
                condition.Surface, condition.ConditionCode, condition.CreatedAt))
            .ToListAsync(ct);
    }

    public Task<ToothCondition?> GetForUpdateAsync(int visitId, int conditionId, CancellationToken ct = default)
        => db.Set<ToothCondition>().FirstOrDefaultAsync(x => x.VisitId == visitId && x.Id == conditionId, ct);

    public async Task AddAsync(ToothCondition condition, CancellationToken ct = default)
        => await db.Set<ToothCondition>().AddAsync(condition, ct);

    public void Remove(ToothCondition condition) => db.Set<ToothCondition>().Remove(condition);
}
