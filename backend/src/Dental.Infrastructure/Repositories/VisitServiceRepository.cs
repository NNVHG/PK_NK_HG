using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class VisitServiceRepository(DentalDbContext db) : IVisitServiceRepository
{
    public async Task<IReadOnlyList<VisitService>> GetForVisitAsync(int visitId, CancellationToken ct = default)
        => await db.VisitServices.AsNoTracking().Where(x => x.VisitId == visitId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync(ct);

    public Task AddRangeAsync(IReadOnlyList<VisitService> rows, CancellationToken ct = default)
        => db.VisitServices.AddRangeAsync(rows, ct);
}
