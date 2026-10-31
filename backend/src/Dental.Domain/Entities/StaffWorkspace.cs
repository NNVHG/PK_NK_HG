using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>MOD_MST / DL-261, DL-362, DL-367: assistant stores only a dentist link; dentist stores daily confirmed room/chair.</summary>
public sealed class StaffWorkspace : BaseEntity
{
    public int UserId { get; set; }
    public int? ChairId { get; set; }
    public int? RoomId { get; set; }
    public DateOnly? WorkDate { get; set; }
    public int? DentistId { get; set; }
    public int Version { get; set; } = 1;
    public int UpdatedByUserId { get; set; }
    public User User { get; set; } = null!;
}
