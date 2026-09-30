using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>
/// Tài khoản đăng nhập. Định danh bằng SĐT.
/// Mật khẩu lưu dạng bcrypt hash — không bao giờ lưu plaintext.
/// </summary>
public class User : BaseEntity
{
    public int UserId { get; set; }

    /// <summary>SĐT — dùng để đăng nhập, phải unique</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>BCrypt hash. Không log, không trả về client.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }

    public int RoleId { get; set; }

    /// <summary>false = tài khoản bị khóa, không cho đăng nhập</summary>
    public bool IsActive { get; set; } = true;

    // Navigation
    public Role Role { get; set; } = null!;
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
