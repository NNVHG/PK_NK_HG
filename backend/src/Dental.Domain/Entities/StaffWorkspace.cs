using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>MOD_MST / DL-261: assistant stores only a dentist link, never a copied chair/room.</summary>
public sealed class StaffWorkspace : BaseEntity
{
    public int UserId { get; set; }
    public int? ChairId { get; set; }
    public int? DentistId { get; set; }
    public int Version { get; set; } = 1;
    public int UpdatedByUserId { get; set; }
    public User User { get; set; } = null!;
}
