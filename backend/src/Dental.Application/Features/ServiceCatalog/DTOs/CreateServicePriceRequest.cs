namespace Dental.Application.Features.ServiceCatalog.DTOs;

public sealed record CreateServicePriceRequest(decimal Amount, DateTimeOffset EffectiveFrom);
