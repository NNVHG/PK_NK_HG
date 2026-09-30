namespace Dental.Application.Features.Staff.DTOs;

public sealed class StaffQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? RoleCode { get; set; }
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
}
