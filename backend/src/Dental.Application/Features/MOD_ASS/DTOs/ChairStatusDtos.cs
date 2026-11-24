using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.MOD_ASS.DTOs;

public sealed record ChairStatusQuery(string? Keyword = null, int? Status = null, int Page = 1, int PageSize = 20, bool? IsActive = null);
public sealed record ChangeChairStatusRequest(int Status, int ExpectedVersion, bool Confirmed);
public sealed record ChairStatusAssistant(int UserId, string FullName);
public sealed record ChairStatusResponse(int Id, string ChairNumber, string RoomName, int Status,
    string StatusText, bool IsActive, int Version, bool IsAssigned, bool HasActiveVisit)
{
    public int? DentistId { get; init; }
    public string? DentistName { get; init; }
    public IReadOnlyList<ChairStatusAssistant> Assistants { get; init; } = [];

    public static string Label(DentalChairStatus status) => status switch
    {
        DentalChairStatus.Available => "Sẵn sàng", DentalChairStatus.Occupied => "Đang phục vụ",
        DentalChairStatus.Maintenance => "Bảo trì", DentalChairStatus.Inactive => "Ngừng sử dụng", _ => "Không xác định"
    };
    public static ChairStatusResponse From(ClinicRoom room) => new(room.Id, room.RoomCode, room.RoomName,
        (int)room.Status, Label(room.Status), room.IsActive, room.Version, false, false);
}
