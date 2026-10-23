namespace Dental.Application.Features.Auth.DTOs;

/// <summary>Dữ liệu đăng nhập: SĐT nhân viên/tài khoản cũ hoặc CCCD bệnh nhân mới, cùng mật khẩu.</summary>
public sealed record LoginRequest(string Phone, string Password);
