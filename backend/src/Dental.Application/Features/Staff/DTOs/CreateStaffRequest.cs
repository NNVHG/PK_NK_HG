namespace Dental.Application.Features.Staff.DTOs;

// DL030/331: Password is retained for a clear validation error for older clients; omit it for new requests.
public sealed record CreateStaffRequest(
    string FullName,
    string Phone,
    string? Password,
    string RoleCode,
    string? Email,
    DateOnly? DateOfBirth,
    string? Gender,
    string? AcademicTitle = null);
