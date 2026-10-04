namespace Dental.Application.Features.ServiceCatalog.DTOs;

public sealed record ServiceCatalogListItemResponse(
    int DentalServiceId,
    string Code,
    string Name,
    string? Description,
    int DurationMinutes,
    bool IsActive,
    decimal? CurrentPrice,
    DateTime? CurrentPriceEffectiveFrom);
