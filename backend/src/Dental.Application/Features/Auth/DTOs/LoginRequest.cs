using Dental.Domain.Enums;

namespace Dental.Application.Features.Auth.DTOs;

/// <summary>
/// Dữ liệu đăng nhập: SĐT nhân viên/tài khoản cũ, CCCD hoặc Hộ chiếu bệnh nhân (DL-360), cùng mật khẩu.
/// </summary>
public sealed record LoginRequest(
    string Phone,
    string Password,
    IdentityDocumentType? DocumentType = null,
    string? NationalityCode = null
);

