namespace Dental.Application.Features.Appointments.DTOs;

public sealed record AppointmentDentistConflictDetails(
    int ConflictAppointmentId,
    string DentistName,
    string PatientName,
    DateOnly AppointmentDate,
    TimeOnly SlotTime,
    int? ConflictRequestId = null);
