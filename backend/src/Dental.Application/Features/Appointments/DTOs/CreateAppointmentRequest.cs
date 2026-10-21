namespace Dental.Application.Features.Appointments.DTOs;

public sealed record CreateAppointmentRequest(
    int PatientId,
    DateOnly AppointmentDate,
    TimeOnly SlotTime,
    int? DentistId = null,
    string? Notes = null,
    bool IsEmergency = false,
    string? EmergencyReason = null,
    int? ConfirmedDentistConflictAppointmentId = null,
    string? ExpectedScheduleRevision = null,
    int? ConfirmedDentistConflictRequestId = null
);
