namespace Dental.Application.Features.Queue.DTOs;

public sealed record QueueEntryDetailResponse(
    QueueEntryResponse Entry,
    IReadOnlyList<QueueStatusHistoryResponse> History
);
