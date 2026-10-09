namespace Dental.Application.Features.Queue.DTOs;

public sealed record QueueEntryResponse(
    int QueueEntryId,
    int PatientId,
    string PatientCode,
    string PatientName,
    string PatientPhone,
    int? AppointmentId,
    int? DentistId,
    string? DentistName,
    int? VisitId,
    DateOnly QueueDate,
    int QueueNumber,
    bool IsPriority,
    DateTime CheckInTime,
    int Status,
    string StatusText,
    string? Notes,
    DateTime CreatedAt
);
