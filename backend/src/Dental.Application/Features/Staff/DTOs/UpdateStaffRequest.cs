namespace Dental.Application.Features.Staff.DTOs;

public sealed record UpdateStaffRequest(
    string FullName,
    string Phone,
    string? Email,
    DateOnly? DateOfBirth,
    string? Gender,
    string RoleCode);
