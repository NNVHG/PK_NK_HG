namespace Dental.Application.Features.Queue.DTOs;

public sealed record CancelWaitingRequest(bool Confirmed, string? Reason = null);
