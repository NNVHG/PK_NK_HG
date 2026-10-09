namespace Dental.Application.Features.Appointments.DTOs;

public sealed record CancelAppointmentRequest(
    string? Reason = null
);
