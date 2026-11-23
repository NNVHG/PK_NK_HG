namespace Dental.Application.Features.Queue.DTOs;

public sealed record QueueQueryRequest(
    DateOnly? Date = null,
    int? Status = null,
    int? DentistId = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record OpenCheckInEntry(int PatientId, int QueueEntryId, int QueueNumber, DateOnly QueueDate);
