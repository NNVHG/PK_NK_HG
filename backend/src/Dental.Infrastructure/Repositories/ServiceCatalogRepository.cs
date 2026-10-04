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
        var items = await query
            .OrderBy(service => service.Name)
            .ThenBy(service => service.DentalServiceId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(service => new ServiceCatalogListItemResponse(
                service.DentalServiceId,
                service.Code,
                service.Name,
                service.Description,
                service.DurationMinutes,
                service.IsActive,
                service.Prices.Where(price => price.EffectiveFrom <= nowUtc)
                    .OrderByDescending(price => price.EffectiveFrom)
                    .ThenByDescending(price => price.ServicePriceId)
                    .Select(price => (decimal?)price.Amount)
                    .FirstOrDefault(),
                service.Prices.Where(price => price.EffectiveFrom <= nowUtc)
                    .OrderByDescending(price => price.EffectiveFrom)
                    .ThenByDescending(price => price.ServicePriceId)
                    .Select(price => (DateTime?)price.EffectiveFrom)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return PagedResult<ServiceCatalogListItemResponse>.Create(items, totalCount, page, pageSize);
    }

    public Task<DentalService?> GetByIdAsync(int serviceId, bool includePrices, CancellationToken ct = default)
    {
        var query = _db.DentalServices.AsNoTracking().AsQueryable();
        if (includePrices)
            query = query.Include(service => service.Prices);
        return query.FirstOrDefaultAsync(service => service.DentalServiceId == serviceId, ct);
    }

    public Task<DentalService?> GetByIdForUpdateAsync(int serviceId, CancellationToken ct = default)
        => _db.DentalServices.FirstOrDefaultAsync(service => service.DentalServiceId == serviceId, ct);

    public Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken ct = default)
        => _db.DentalServices.AnyAsync(service => service.Code == normalizedCode, ct);

    public Task<DateTime?> GetLatestPriceEffectiveFromAsync(int serviceId, CancellationToken ct = default)
        => _db.ServicePrices
            .Where(price => price.ServiceId == serviceId)
            .Select(price => (DateTime?)price.EffectiveFrom)
            .MaxAsync(ct);

    public async Task<bool> AddServiceWithInitialPriceAsync(
        DentalService service, ServicePrice initialPrice, CancellationToken ct = default)
    {
        _db.DentalServices.Add(service);
        _db.ServicePrices.Add(initialPrice);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException exception) when (IsServiceCodeConflict(exception))
        {
            _db.Entry(service).State = EntityState.Detached;
            _db.Entry(initialPrice).State = EntityState.Detached;
            return false;
        }
    }

    public async Task AddPriceAsync(ServicePrice price, CancellationToken ct = default)
        => await _db.ServicePrices.AddAsync(price, ct);

    public async Task<ActiveServiceWithCurrentPriceResponse?> GetActiveServiceWithCurrentPriceAsync(
        int serviceId, DateTime nowUtc, CancellationToken ct = default)
        => await _db.DentalServices.AsNoTracking()
            .Where(service => service.DentalServiceId == serviceId && service.IsActive)
            .Select(service => new
            {
                Service = service,
                Price = service.Prices.Where(price => price.EffectiveFrom <= nowUtc)
                    .OrderByDescending(price => price.EffectiveFrom)
                    .ThenByDescending(price => price.ServicePriceId)
                    .Select(price => new { price.Amount, price.EffectiveFrom })
                    .FirstOrDefault(),
            })
            .Where(item => item.Price != null)
            .Select(item => new ActiveServiceWithCurrentPriceResponse(
                item.Service.DentalServiceId,
                item.Service.Code,
                item.Service.Name,
                item.Service.DurationMinutes,
                item.Price!.Amount,
                item.Price.EffectiveFrom))
            .FirstOrDefaultAsync(ct);

    private static bool IsServiceCodeConflict(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_DentalServices_Code",
        };
}
