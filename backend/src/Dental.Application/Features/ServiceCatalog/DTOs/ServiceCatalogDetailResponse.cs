namespace Dental.Application.Features.ServiceCatalog.DTOs;

public sealed record ServiceCatalogDetailResponse(
    int DentalServiceId,
    string Code,
    string Name,
    string? Description,
    int DurationMinutes,
    bool IsActive,
    decimal? CurrentPrice,
    DateTime? CurrentPriceEffectiveFrom,
    IReadOnlyList<ServicePriceResponse>? PriceHistory);
