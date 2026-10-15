using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>MOD_MST / DL-104: flat chair catalog; room is a name, not a separate hierarchy.</summary>
public sealed class DentalChair : BaseEntity
{
    public int Id { get; set; }
    public string ChairNumber { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DentalChairStatus Status { get; set; } = DentalChairStatus.Available;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int CreatedByUserId { get; set; }
    public int? UpdatedByUserId { get; set; }
    public int Version { get; set; } = 1;
}
