using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.MOD_ASS.DTOs;

public sealed record ChairStatusQuery(string? Keyword = null, int? Status = null, int Page = 1, int PageSize = 20);
public sealed record ChangeChairStatusRequest(int Status, int ExpectedVersion, bool Confirmed);
public sealed record ChairStatusResponse(int Id, string ChairNumber, string RoomName, int Status,
    string StatusText, bool IsActive, int Version, bool IsAssigned, bool HasActiveVisit)
{
    public static string Label(DentalChairStatus status) => status switch
    {
        DentalChairStatus.Available => "Sẵn sàng", DentalChairStatus.Occupied => "Đang phục vụ",
        DentalChairStatus.Maintenance => "Bảo trì", DentalChairStatus.Inactive => "Ngừng sử dụng", _ => "Không xác định"
    };
    public static ChairStatusResponse From(DentalChair chair) => new(chair.Id, chair.ChairNumber, chair.RoomName,
        (int)chair.Status, Label(chair.Status), chair.IsActive, chair.Version, false, false);
}
