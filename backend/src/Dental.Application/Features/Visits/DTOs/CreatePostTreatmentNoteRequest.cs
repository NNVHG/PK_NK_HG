namespace Dental.Application.Features.Visits.DTOs;

public sealed record CreatePostTreatmentNoteRequest(
    string Note,
    Guid? RequestId = null);
