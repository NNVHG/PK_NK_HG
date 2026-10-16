namespace Dental.Application.Features.Queue.DTOs;

public sealed record PublicQueueItemResponse(
    int QueueEntryId,
    int QueueNumber,
    string QueueNumberDisplay,
    string MaskedPatientName,
    string? DentistName,
    int Status,
    string StatusText,
    bool IsPriority,
    string? ChairNumber = null,
    string? RoomName = null
);
