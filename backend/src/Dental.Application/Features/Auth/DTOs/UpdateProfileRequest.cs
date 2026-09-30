namespace Dental.Application.Features.Auth.DTOs;

/// <summary>Thông tin hồ sơ người dùng được phép cập nhật.</summary>
public sealed record UpdateProfileRequest(
    string FullName,
    string Phone,
    string? Email,
    DateOnly? DateOfBirth,
    string? Gender
);
