using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>
/// Tài khoản đăng nhập; nhân viên/tài khoản cũ dùng SĐT, Patient mới có thể dùng CCCD.
/// Mật khẩu lưu dạng bcrypt hash — không bao giờ lưu plaintext.
/// </summary>
public class User : BaseEntity
{
    public int UserId { get; set; }

    /// <summary>SĐT liên hệ; có thể dùng chung bởi tài khoản đăng nhập CCCD.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Patient accounts created under DL-340 authenticate with the linked Patient's CCCD hash.</summary>
    public bool LoginByCitizenId { get; set; }
    /// <summary>Marks pre-DL-340 family accounts so their last remaining Patient is still split at the counter.</summary>
    public bool IsLegacyFamilyAccount { get; set; }

    /// <summary>BCrypt hash. Không log, không trả về client.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Incremented after SMS recovery so previously issued access tokens no longer authorize requests.</summary>
    public int TokenVersion { get; set; }

    /// <summary>New approved patients and newly created staff must complete first-login OTP before receiving a JWT.</summary>
    public bool RequiresPhoneActivation { get; set; }
    public DateTime? PhoneVerifiedAtUtc { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? AcademicTitle { get; set; }
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
