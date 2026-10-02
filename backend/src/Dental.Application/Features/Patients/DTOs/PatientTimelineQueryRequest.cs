namespace Dental.Application.Features.Patients.DTOs;

public sealed class PatientTimelineQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
