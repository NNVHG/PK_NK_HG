namespace Dental.Application.Features.Queue.DTOs;

public sealed record QueueCleanupResponse(int CancelledCount, int RolledOverCount, DateOnly LastExpiredDate);
