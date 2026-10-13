namespace Dental.Application.Features.Appointments.DTOs;

public sealed record RescheduleAppointmentRequest(
    DateOnly AppointmentDate,
    TimeOnly SlotTime,
    int? DentistId = null,
    string? Notes = null,
    string? Reason = null
);
