namespace Dental.Application.Features.Queue.DTOs;

public sealed record PublicQueueItemResponse(
    string QueueNumberDisplay,
    string MaskedPatientName,
    int Status,
    string? RoomName = null
);
