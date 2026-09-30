using Dental.Application.Interfaces;
using Dental.Infrastructure.Data;

namespace Dental.Infrastructure.Data;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DentalDbContext _db;

    public UnitOfWork(DentalDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
