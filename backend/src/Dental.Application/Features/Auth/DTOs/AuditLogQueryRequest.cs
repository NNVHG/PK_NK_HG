namespace Dental.Application.Features.Auth.DTOs;

public sealed class AuditLogQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? PerformedBy { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}
