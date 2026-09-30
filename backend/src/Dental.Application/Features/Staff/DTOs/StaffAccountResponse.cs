namespace Dental.Application.Features.Staff.DTOs;

public sealed record StaffAccountResponse(
    int UserId,
    string Phone,
    string FullName,
    string? Email,
    DateOnly? DateOfBirth,
    string? Gender,
    string RoleCode,
    string RoleName,
    bool IsActive);
