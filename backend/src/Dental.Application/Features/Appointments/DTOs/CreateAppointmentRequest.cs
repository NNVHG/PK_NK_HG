namespace Dental.Application.Features.Appointments.DTOs;

public sealed record CreateAppointmentRequest(
    int PatientId,
    DateOnly AppointmentDate,
    TimeOnly SlotTime,
    int? DentistId = null,
    string? Notes = null,
    // Retired by DL337; kept so old clients receive an explicit validation error.
    bool IsEmergency = false,
    string? EmergencyReason = null,
    int? ConfirmedDentistConflictAppointmentId = null,
    string? ExpectedScheduleRevision = null,
    int? ConfirmedDentistConflictRequestId = null
);
