namespace Dental.Application.Features.ServiceCatalog.DTOs;

public sealed record UpdateDentalServiceRequest(string Name, string? Description, int DurationMinutes);
