namespace Dental.Application.Features.Patients.DTOs;

public sealed record PatientSafetyAlertsResponse(
    bool HasHistory,
    IReadOnlyList<PatientSafetyAlertItemResponse> Alerts);

public sealed record PatientSafetyAlertItemResponse(
    string Type,
    string Name,
    string? Detail,
    DateTime RecordedAt,
    int VisitId);
