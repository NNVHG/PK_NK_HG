namespace Dental.Application.Features.Queue.DTOs;

public sealed record QueueQueryRequest(
    DateOnly? Date = null,
    int? Status = null,
    int? DentistId = null,
    int Page = 1,
    int PageSize = 20
);
