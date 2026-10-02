using System.Globalization;
using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>Hồ sơ bệnh nhân, không đồng nhất với tài khoản đăng nhập.</summary>
public sealed class Patient : BaseEntity
{
    public int PatientId { get; set; }
    public int PatientNumber { get; set; }

    /// <summary>Mã hiển thị được suy ra từ số bệnh nhân, không lưu thành cột.</summary>
    public string PatientCode => "BN" + PatientNumber.ToString("D6", CultureInfo.InvariantCulture);

    public string FullName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? Gender { get; set; }

    // [CẦN XÁC NHẬN] Cho phép hồ sơ người nhà dùng chung số điện thoại; không áp unique cho Phone.
    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Address { get; set; }
    public int? UserId { get; set; }
    public bool IsActive { get; set; } = true;

    public User? User { get; set; }
}
