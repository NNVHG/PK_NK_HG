using System.Globalization;
using Dental.Domain.Common;
using Dental.Domain.Enums;

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

    // DL-340: nhiều tài khoản Patient có thể dùng chung số liên hệ; tài khoản mới đăng nhập bằng CCCD.
    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? CitizenIdHash { get; set; }
    public string? CitizenIdMasked { get; set; }
    public IdentityDocumentType? DocumentType { get; set; }
    public string? IssuingCountryCode { get; set; }
    public IdentityHashScheme? HashScheme { get; set; }
    public int? IdentityKeyVersion { get; set; }
    public int CitizenIdVersion { get; set; }
    public int? UserId { get; set; }
    public bool IsActive { get; set; } = true;

    public User? User { get; set; }
}
