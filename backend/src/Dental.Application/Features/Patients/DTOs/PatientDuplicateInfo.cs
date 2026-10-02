namespace Dental.Application.Features.Patients.DTOs;

public sealed record PatientDuplicateInfo(
    string PatientCode,
    string FullName,
    DateOnly DateOfBirth,
    string Phone);
