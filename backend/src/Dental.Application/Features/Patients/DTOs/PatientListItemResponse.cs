namespace Dental.Application.Features.Patients.DTOs;

public sealed record PatientListItemResponse(
    string PatientCode,
    string FullName,
    DateOnly DateOfBirth,
    string? Gender,
    string Phone);
