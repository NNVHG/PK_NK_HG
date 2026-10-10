using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class ToothConditionRepository(DentalDbContext db) : IToothConditionRepository
{
    public async Task<IReadOnlyList<ToothCondition>> GetForVisitAsync(int visitId, CancellationToken ct = default)
        => await db.Set<ToothCondition>().AsNoTracking().Where(x => x.VisitId == visitId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync(ct);

    public async Task AddAsync(ToothCondition condition, CancellationToken ct = default)
        => await db.Set<ToothCondition>().AddAsync(condition, ct);
}
