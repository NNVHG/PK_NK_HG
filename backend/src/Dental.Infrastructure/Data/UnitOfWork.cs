using Dental.Application.Interfaces;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Dental.Application.Common;
using System.Data;
using Npgsql;

namespace Dental.Infrastructure.Data;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DentalDbContext _db;

    public UnitOfWork(DentalDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public async Task<T> ExecuteSerializableTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await operation(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (Exception error)
        {
            var concurrent = false;
            for (Exception? current = error; current is not null; current = current.InnerException)
                if (current is PostgresException { SqlState: "40001" or "40P01" } or
                    PostgresException { SqlState: "23505", ConstraintName: "IX_NotificationLogs_ReminderOccurrence" }) { concurrent = true; break; }
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (InvalidOperationException) when (concurrent)
            {
                // PostgreSQL may already have rolled back a failed Serializable commit.
            }
            if (concurrent) throw new ConcurrentOperationException();
            throw;
        }
        finally { _db.ChangeTracker.Clear(); }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await operation(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
