namespace Dental.Application.Features.Patients.DTOs;

public sealed record AssignPatientGuardianRequest(
    int GuardianPatientId,
    int WardPatientId,
    string Relationship,
    string? Notes = null
);

public sealed record RevokePatientGuardianRequest(
    int PatientGuardianId,
    DateTime ExpectedAssignedAt,
    string? Notes = null
);

public sealed record PromoteWardRequest(
    string RawCitizenId,
    string? Phone = null,
    string? Notes = null
);

public sealed record PatientGuardianResponse(
    int PatientGuardianId,
    int GuardianPatientId,
    string GuardianPatientCode,
    string GuardianFullName,
    int WardPatientId,
    string WardPatientCode,
    string WardFullName,
    string Relationship,
    bool IsActive,
    DateTime AssignedAt,
    int AssignedByUserId,
    string AssignedByUserName,
    DateTime? RevokedAt,
    int? RevokedByUserId,
    string? RevokedByUserName,
    string? Notes
);

public sealed record WardListItemResponse(
    int WardPatientId,
    string WardPatientCode,
    string FullName,
    DateOnly DateOfBirth,
    string? Gender,
    string Relationship,
    DateTime AssignedAt
);
