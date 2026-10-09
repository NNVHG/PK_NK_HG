namespace Dental.Application.Features.Queue.DTOs;

public sealed record UpdateQueueStatusRequest(
    int NewStatus,
    int? DentistId = null,
    string? Reason = null
);
