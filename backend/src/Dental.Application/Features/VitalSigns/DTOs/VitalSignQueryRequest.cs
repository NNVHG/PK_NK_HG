namespace Dental.Application.Features.VitalSigns.DTOs;

public sealed class VitalSignQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
