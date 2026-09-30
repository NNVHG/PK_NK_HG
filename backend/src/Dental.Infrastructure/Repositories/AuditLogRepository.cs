using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly DentalDbContext _db;

    public AuditLogRepository(DentalDbContext db) => _db = db;

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPageAsync(
        int? performedByUserId,
        string? performedByName,
        string? action,
        string? entityType,
        DateTime? createdFromUtc,
        DateTime? createdBeforeUtc,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.AuditLogs
            .AsNoTracking()
            .Include(log => log.User)
            .AsQueryable();

        if (performedByUserId.HasValue && !string.IsNullOrWhiteSpace(performedByName))
        {
            var userId = performedByUserId.Value;
            var pattern = $"%{performedByName.Trim()}%";
            query = query.Where(log => log.UserId == userId ||
                (log.User != null && EF.Functions.ILike(log.User.FullName, pattern)));
        }
        else if (performedByUserId.HasValue)
        {
            var userId = performedByUserId.Value;
            query = query.Where(log => log.UserId == userId);
        }
        else if (!string.IsNullOrWhiteSpace(performedByName))
        {
            var pattern = $"%{performedByName.Trim()}%";
            query = query.Where(log => log.User != null && EF.Functions.ILike(log.User.FullName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(log => log.Action == action);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(log => EF.Functions.ILike(log.EntityType, $"%{entityType.Trim()}%"));

        if (createdFromUtc.HasValue)
            query = query.Where(log => log.CreatedAt >= createdFromUtc.Value);

        if (createdBeforeUtc.HasValue)
            query = query.Where(log => log.CreatedAt < createdBeforeUtc.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.LogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
