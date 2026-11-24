using System.Text.RegularExpressions;
using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>MOD_MST / MOD_ASS: danh mục phòng điều trị nha khoa.</summary>
public sealed class ClinicRoom : BaseEntity
{
    public int Id { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string NormalizedRoomName { get; set; } = string.Empty;
    public DentalChairStatus Status { get; set; } = DentalChairStatus.Available;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int CreatedByUserId { get; set; }
    public int? UpdatedByUserId { get; set; }
    public int Version { get; set; } = 1;

    public static string CleanDisplayRoomName(string? roomName)
        => string.IsNullOrWhiteSpace(roomName) ? string.Empty : Regex.Replace(roomName.Trim(), @"\s+", " ");

    public static string NormalizeRoomName(string? roomName)
        => CleanDisplayRoomName(roomName).ToUpperInvariant();
}
