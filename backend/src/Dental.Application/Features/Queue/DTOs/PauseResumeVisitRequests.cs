namespace Dental.Application.Features.Queue.DTOs;

public sealed record PauseVisitRequest(bool Confirmed, string? Reason = null);
public sealed record ResumeVisitRequest(bool Confirmed, int? DentistId = null, string? Reason = null);
