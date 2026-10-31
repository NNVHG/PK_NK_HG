using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>MOD_MST / DL-104, DL-361: flat chair catalog preserved for historical traceability; linked to ClinicRoom via RoomId.</summary>
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
    public int? RoomId { get; set; }
    public int Version { get; set; } = 1;
}
