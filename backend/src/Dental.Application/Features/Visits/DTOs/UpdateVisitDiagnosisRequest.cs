namespace Dental.Application.Features.Visits.DTOs;

public sealed record UpdateVisitDiagnosisRequest(
    string Diagnosis,
    string? ClinicalNotes = null);
