namespace Dental.Application.Features.ServiceCatalog.DTOs;

public sealed record CreateDentalServiceRequest(
    string Code,
    string Name,
    string? Description,
    int DurationMinutes,
    decimal InitialPrice,
    DateTimeOffset EffectiveFrom);
