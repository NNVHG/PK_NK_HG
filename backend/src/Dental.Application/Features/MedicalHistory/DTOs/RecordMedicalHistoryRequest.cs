namespace Dental.Application.Features.MedicalHistory.DTOs;

public sealed class RecordMedicalHistoryRequest
{
    public string? Note { get; init; }
    public List<MedicalHistoryItemRequest> Items { get; init; } = [];
}
