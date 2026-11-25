using Dental.Application.Common;
using Dental.Application.Features.ServiceCatalog.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class ServiceCatalogRepository : IServiceCatalogRepository
{
    private readonly DentalDbContext _db;

    public ServiceCatalogRepository(DentalDbContext db) => _db = db;

    public async Task<PagedResult<ServiceCatalogListItemResponse>> GetPageAsync(
        string? keyword,
        bool? isActive,
        int page,
        int pageSize,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var query = _db.DentalServices.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim().ToLower();
            query = query.Where(service => service.Code.ToLower().Contains(normalizedKeyword) ||
                                           service.Name.ToLower().Contains(normalizedKeyword));
        }
        if (isActive is bool active)
            query = query.Where(service => service.IsActive == active);

        var totalCount = await query.CountAsync(ct);
        var services = await query
            .OrderBy(service => service.Name)
            .ThenBy(service => service.DentalServiceId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = services.Select(service =>
        {
            var current = ResolveCurrentPrice(service, nowUtc);
            return new ServiceCatalogListItemResponse(
                service.DentalServiceId,
                service.Code,
                service.Name,
                service.Description,
                service.DurationMinutes,
                service.IsActive,
                current?.Amount,
                current?.EffectiveFrom);
        }).ToList();

        return PagedResult<ServiceCatalogListItemResponse>.Create(items, totalCount, page, pageSize);
    }

    public async Task<DentalService?> GetByIdAsync(int serviceId, bool includePrices, CancellationToken ct = default)
    {
        var service = await _db.DentalServices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DentalServiceId == serviceId, ct);
        if (service is not null && includePrices)
            service.HydratePrices();
        return service;
    }

    public Task<DentalService?> GetByIdForUpdateAsync(int serviceId, CancellationToken ct = default)
        => _db.DentalServices.FirstOrDefaultAsync(service => service.DentalServiceId == serviceId, ct);

    public Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken ct = default)
        => _db.DentalServices.AnyAsync(service => service.Code == normalizedCode, ct);

    public async Task<DateTime?> GetLatestPriceEffectiveFromAsync(int serviceId, CancellationToken ct = default)
    {
        var service = await _db.DentalServices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DentalServiceId == serviceId, ct);
        if (service is null) return null;

        var history = service.GetPriceHistory();
        return history.Count > 0 ? history.Max(x => x.EffectiveFrom) : service.PriceEffectiveFrom;
    }

    public async Task<bool> AddServiceWithInitialPriceAsync(
        DentalService service, ServicePrice initialPrice, CancellationToken ct = default)
    {
        service.AppendPrice(initialPrice);
        _db.DentalServices.Add(service);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException exception) when (IsServiceCodeConflict(exception))
        {
            _db.Entry(service).State = EntityState.Detached;
            return false;
        }
    }

    public async Task AddPriceAsync(ServicePrice price, CancellationToken ct = default)
    {
        var service = await _db.DentalServices
            .FirstOrDefaultAsync(x => x.DentalServiceId == price.ServiceId, ct);
        if (service is null) return;
        service.AppendPrice(price);
    }

    public async Task<ActiveServiceWithCurrentPriceResponse?> GetActiveServiceWithCurrentPriceAsync(
        int serviceId, DateTime nowUtc, CancellationToken ct = default)
    {
        var service = await _db.DentalServices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DentalServiceId == serviceId && x.IsActive, ct);
        if (service is null) return null;

        var current = ResolveCurrentPrice(service, nowUtc);
        if (current is null) return null;

        return new ActiveServiceWithCurrentPriceResponse(
            service.DentalServiceId,
            service.Code,
            service.Name,
            service.DurationMinutes,
            current.Value.Amount,
            current.Value.EffectiveFrom);
    }

    private static (decimal Amount, DateTime EffectiveFrom)? ResolveCurrentPrice(DentalService service, DateTime nowUtc)
    {
        var history = service.GetPriceHistory();
        if (history.Count > 0)
        {
            var match = history
                .Where(price => price.EffectiveFrom <= nowUtc)
                .OrderByDescending(price => price.EffectiveFrom)
                .ThenByDescending(price => price.ServicePriceId)
                .FirstOrDefault();
            return match is null ? null : (match.Amount, match.EffectiveFrom);
        }

        if (service.PriceEffectiveFrom is DateTime effective && effective <= nowUtc)
            return (service.UnitPrice, effective);

        return null;
    }

    private static bool IsServiceCodeConflict(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_DentalServices_Code",
        };
}
