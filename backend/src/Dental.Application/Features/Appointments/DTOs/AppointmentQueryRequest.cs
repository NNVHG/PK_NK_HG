namespace Dental.Application.Features.Appointments.DTOs;

public sealed record AppointmentQueryRequest(
    DateOnly? Date = null,
    int? PatientId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20
);
