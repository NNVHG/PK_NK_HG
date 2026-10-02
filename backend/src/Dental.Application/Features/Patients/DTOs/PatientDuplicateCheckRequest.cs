namespace Dental.Application.Features.Patients.DTOs;

public sealed record PatientDuplicateCheckRequest(
    string FullName,
    DateOnly DateOfBirth,
    string Phone);
