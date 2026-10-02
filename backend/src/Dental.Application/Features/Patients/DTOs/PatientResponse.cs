namespace Dental.Application.Features.Patients.DTOs;

public sealed record PatientResponse(
    int PatientId,
    string PatientCode,
    string FullName,
    DateOnly DateOfBirth,
    string? Gender,
    string Phone,
    string? Email,
    string? Address,
    bool IsActive);
