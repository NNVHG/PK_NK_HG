namespace Dental.Application.Features.Queue.DTOs;

public sealed record QueueStatusHistoryResponse(
    int QueueStatusHistoryId,
    int QueueEntryId,
    int? FromStatus,
    string? FromStatusText,
    int ToStatus,
    string ToStatusText,
    int ChangedByUserId,
    string ChangedByUserName,
    DateTime ChangedAt,
    string? Reason
);
