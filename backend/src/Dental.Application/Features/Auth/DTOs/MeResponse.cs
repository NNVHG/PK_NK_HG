namespace Dental.Application.Features.Auth.DTOs;

/// <summary>Thông tin bản thân của người dùng đang đăng nhập.</summary>
public sealed record MeResponse(
    int UserId,
    string Phone,
    string FullName,
    string? Email,
    string RoleCode,
    string RoleName,
    bool IsActive
);
