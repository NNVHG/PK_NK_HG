namespace Dental.Application.Features.Appointments.DTOs;

public sealed record SelfCancellationEligibilityResponse(int AppointmentId, bool CanCancel, string? Message, int ReasonMaxLength);
