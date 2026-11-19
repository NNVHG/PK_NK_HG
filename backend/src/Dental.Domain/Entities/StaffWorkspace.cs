using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>Daily staff assignment. An assistant confirms optional dentist support and follows the dentist's room.</summary>
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
