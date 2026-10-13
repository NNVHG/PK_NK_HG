namespace Dental.Application.Features.Appointments.DTOs;

public sealed record SelfReschedulingEligibilityResponse(
    AppointmentResponse Appointment, bool CanReschedule, string? Message, int RemainingChanges);
