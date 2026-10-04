namespace Dental.Application.Features.ServiceCatalog.DTOs;

public sealed class ServiceCatalogQueryRequest
{
    public string? Keyword { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
