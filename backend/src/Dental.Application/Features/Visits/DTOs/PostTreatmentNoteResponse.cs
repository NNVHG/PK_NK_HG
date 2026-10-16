namespace Dental.Application.Features.Visits.DTOs;

public sealed record PostTreatmentNoteResponse(
    int Id,
    int VisitId,
    int CreatedByUserId,
    string CreatedByFullName,
    string CreatedByRoleName,
    string Note,
    DateTime CreatedAt,
    Guid? RequestId);
