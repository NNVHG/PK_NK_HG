using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>MOD_MST / DL-361, DL-367: phòng điều trị độc lập được tách từ danh mục ghế lịch sử.</summary>
public sealed class ClinicRoom : BaseEntity
{
    public int Id { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string NormalizedRoomName { get; set; } = string.Empty;
    public DentalChairStatus Status { get; set; } = DentalChairStatus.Available;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int? LegacyChairId { get; set; }
    public int CreatedByUserId { get; set; }
    public int? UpdatedByUserId { get; set; }
    public int Version { get; set; } = 1;
}
