using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>
/// Quản lý quan hệ giám hộ giữa người giám hộ và trẻ/người được giám hộ (DL-353, DL-358, DL-359).
/// Một giám hộ quản lý nhiều trẻ, một trẻ chỉ có tối đa một giám hộ hiệu lực.
/// </summary>
public sealed class PatientGuardian : BaseEntity
{
    public int PatientGuardianId { get; set; }

    /// <summary>ID hồ sơ bệnh nhân của người giám hộ.</summary>
    public int GuardianPatientId { get; set; }

    /// <summary>ID hồ sơ bệnh nhân của người được giám hộ (trẻ).</summary>
    public int WardPatientId { get; set; }

    /// <summary>Quan hệ (Cha, Mẹ, Người giám hộ hợp pháp...).</summary>
    public string Relationship { get; set; } = string.Empty;

    /// <summary>Trạng thái hiệu lực của quan hệ giám hộ.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Thời điểm gán quyền giám hộ.</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Nhân viên thực hiện gán quyền tại quầy.</summary>
    public int AssignedByUserId { get; set; }

    /// <summary>Thời điểm thu hồi quyền giám hộ.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Nhân viên thực hiện thu hồi quyền tại quầy.</summary>
    public int? RevokedByUserId { get; set; }

    /// <summary>Ghi chú về giấy tờ hoặc lý do gán/thu hồi.</summary>
    public string? Notes { get; set; }

    // Navigation properties
    public Patient? GuardianPatient { get; set; }
    public Patient? WardPatient { get; set; }
    public User? AssignedByUser { get; set; }
    public User? RevokedByUser { get; set; }
}
