namespace Dental.Application.Features.MedicalHistory.DTOs;

public sealed record MedicalHistoryRecordResponse(
    int RecordId,
    int PatientId,
    int VisitId,
    string? Note,
    int RecordedByUserId,
    DateTime CreatedAt,
    IReadOnlyList<MedicalHistoryItemResponse> Items);

public sealed record MedicalHistoryItemResponse(
    int ItemId,
    string Type,
    string Name,
    bool IsCritical,
    string? Detail);
