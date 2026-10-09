namespace Dental.Application.Features.Appointments.DTOs;

public sealed record AppointmentResponse(
    int AppointmentId,
    int PatientId,
    string PatientCode,
    string PatientName,
    string PatientPhone,
    DateOnly AppointmentDate,
    TimeOnly SlotTime,
    int? DentistId,
    string? DentistName,
    string Status,
    string? Notes,
    int CreatedByUserId,
    DateTime CreatedAt
);
