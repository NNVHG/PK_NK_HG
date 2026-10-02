namespace Dental.Application.Features.MedicalHistory.DTOs;

public sealed record MedicalHistoryItemRequest(
    string Type,
    string Name,
    bool IsCritical,
    string? Detail);
