namespace Dental.Application.Features.Patients.DTOs;

public sealed class PatientQueryRequest
{
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
