namespace Dental.Application.Features.Queue.DTOs;

public sealed record PublicQueueMonitorResponse(
    IReadOnlyList<PublicQueueItemResponse> InConsultation,
    IReadOnlyList<PublicQueueItemResponse> Waiting
);
