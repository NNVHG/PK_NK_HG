namespace Dental.Application.Features.Queue.DTOs;

public sealed record QueueCleanupRequest(bool Confirmed);
public sealed record QueueCleanupResponse(int CancelledCount, DateOnly LastExpiredDate);
