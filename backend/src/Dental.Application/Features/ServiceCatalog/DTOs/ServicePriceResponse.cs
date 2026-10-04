namespace Dental.Application.Features.ServiceCatalog.DTOs;

public sealed record ServicePriceResponse(
    int ServicePriceId,
    decimal Amount,
    DateTime EffectiveFrom,
    int CreatedByUserId,
    DateTime CreatedAt);
