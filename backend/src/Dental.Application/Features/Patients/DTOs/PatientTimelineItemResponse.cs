namespace Dental.Application.Features.Patients.DTOs;

public sealed record PatientTimelineItemResponse(
    int VisitId,
    string Status,
    DateTime? StartedAt,
    DateTime? EndedAt,
    string? DentistName,
    bool HasMedicalHistory,
    bool HasVitalSigns,
    string? Diagnosis = null);
