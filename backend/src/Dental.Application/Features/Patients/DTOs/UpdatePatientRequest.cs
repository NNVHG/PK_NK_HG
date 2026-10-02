namespace Dental.Application.Features.Patients.DTOs;

public sealed record UpdatePatientRequest(
    string FullName,
    DateOnly DateOfBirth,
    string? Gender,
    string Phone,
    string? Email,
    string? Address,
    bool ConfirmNotDuplicate = false);
