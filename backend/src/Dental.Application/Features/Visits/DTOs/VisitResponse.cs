namespace Dental.Application.Features.Visits.DTOs;

public sealed record VisitResponse(
    int VisitId,
    int PatientId,
    string Status,
    DateTime? StartedAt,
    DateTime? EndedAt,
    int? DentistId,
    int CreatedByUserId,
    DateTime CreatedAt);
