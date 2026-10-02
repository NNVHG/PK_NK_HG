namespace Dental.Application.Features.Visits.DTOs;

public sealed class VisitQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
