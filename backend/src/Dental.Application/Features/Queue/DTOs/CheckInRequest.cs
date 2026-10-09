namespace Dental.Application.Features.Queue.DTOs;

public sealed record CheckInRequest(
    int PatientId,
    int? AppointmentId = null,
    int? DentistId = null,
    string? Notes = null
);
