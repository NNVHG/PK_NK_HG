namespace Dental.Application.Features.Patients.DTOs;

public sealed record CreatePatientRequest(
    string FullName,
    DateOnly DateOfBirth,
    string? Gender,
    string Phone,
    string? Email = null,
    string? Address = null,
    bool ConfirmNotDuplicate = false);
