namespace Dental.Application.Features.Queue.DTOs;

public sealed record QueueAbsenceRequest(bool Confirmed, string? Reason = null);
