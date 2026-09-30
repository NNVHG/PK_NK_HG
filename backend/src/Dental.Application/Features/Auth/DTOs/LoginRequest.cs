namespace Dental.Application.Features.Auth.DTOs;

/// <summary>Dữ liệu đăng nhập: số điện thoại + mật khẩu.</summary>
public sealed record LoginRequest(string Phone, string Password);
