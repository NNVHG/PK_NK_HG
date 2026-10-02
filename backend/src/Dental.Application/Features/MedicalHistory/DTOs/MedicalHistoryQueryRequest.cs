namespace Dental.Application.Features.MedicalHistory.DTOs;

public sealed class MedicalHistoryQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
