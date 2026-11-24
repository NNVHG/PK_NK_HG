using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>Lưu lựa chọn phòng điều trị trong ngày của Nha sĩ và liên kết đi theo Nha sĩ của Phụ tá (Admin chỉ được gỡ liên kết).</summary>
public sealed class StaffWorkspace : BaseEntity
{
    public int UserId { get; set; }
    public int? RoomId { get; set; }
    public DateOnly? WorkDate { get; set; }
    public int? DentistId { get; set; }
    public int Version { get; set; } = 1;
    public int UpdatedByUserId { get; set; }
    public User User { get; set; } = null!;
}
