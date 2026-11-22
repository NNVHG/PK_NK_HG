namespace Dental.Application.Features.Patients.DTOs;

public sealed record PatientListItemResponse(
    int PatientId,
    string PatientCode,
    string FullName,
    DateOnly DateOfBirth,
    string? Gender,
    string Phone,
    bool HasUserAccount = false,
    bool HasActiveGuardian = false,
    bool IsUnderFourteen = false,
    bool MissingGuardianAccount = false);
