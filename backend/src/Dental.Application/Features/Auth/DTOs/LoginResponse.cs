namespace Dental.Application.Features.Auth.DTOs;

/// <summary>Phản hồi đăng nhập thành công.</summary>
public sealed record LoginResponse(
    string AccessToken,
    int UserId,
    string FullName,
    string RoleCode,
    string RoleName
);
