namespace Dental.Application.Features.MOD_MST.DTOs;

public sealed record DentalChairQuery(string? Keyword = null, int? Status = null, bool? IsActive = null, int Page = 1, int PageSize = 20);
public sealed record CreateDentalChairRequest(string ChairNumber, string RoomName, int Status, string? Description, bool Confirmed);
public sealed record UpdateDentalChairRequest(string RoomName, int Status, string? Description, int ExpectedVersion, bool Confirmed);
public sealed record DentalChairResponse(int Id, string ChairNumber, string RoomName, int Status, string StatusText,
    string? Description, bool IsActive, int Version, DateTime CreatedAt, DateTime? UpdatedAt);
