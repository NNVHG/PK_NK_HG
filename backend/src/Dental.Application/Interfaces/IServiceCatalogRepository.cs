using Dental.Application.Common;
using Dental.Application.Features.ServiceCatalog.DTOs;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IServiceCatalogRepository
{
    Task<PagedResult<ServiceCatalogListItemResponse>> GetPageAsync(
        string? keyword, bool? isActive, int page, int pageSize, DateTime nowUtc, CancellationToken ct = default);
    Task<DentalService?> GetByIdAsync(int serviceId, bool includePrices, CancellationToken ct = default);
    Task<DentalService?> GetByIdForUpdateAsync(int serviceId, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken ct = default);
    Task<DateTime?> GetLatestPriceEffectiveFromAsync(int serviceId, CancellationToken ct = default);
    Task<bool> AddServiceWithInitialPriceAsync(DentalService service, ServicePrice initialPrice, CancellationToken ct = default);
    Task AddPriceAsync(ServicePrice price, CancellationToken ct = default);
    Task<ActiveServiceWithCurrentPriceResponse?> GetActiveServiceWithCurrentPriceAsync(
        int serviceId, DateTime nowUtc, CancellationToken ct = default);
}
