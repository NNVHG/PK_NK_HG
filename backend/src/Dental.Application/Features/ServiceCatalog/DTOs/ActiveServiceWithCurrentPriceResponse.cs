namespace Dental.Application.Features.ServiceCatalog.DTOs;

public sealed record ActiveServiceWithCurrentPriceResponse(
    int DentalServiceId,
    string Code,
    string Name,
    int DurationMinutes,
    decimal CurrentPrice,
    DateTime CurrentPriceEffectiveFrom);
