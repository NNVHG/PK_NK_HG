namespace Dental.Application.Features.MOD_MST.DTOs;

public sealed record MedicalSupplyQuery(string? Keyword = null, int Page = 1, int PageSize = 20, bool IncludeInactive = false);

public sealed record MedicalSupplyWriteRequest(
    string Code,
    string Name,
    string Unit,
    int MinimumStockThreshold,
    string? Description = null,
    bool IsActive = true,
    int ExpectedVersion = 0);

public sealed record MedicalSupplyResponse(
    int Id,
    string Code,
    string Name,
    string Unit,
    int MinimumStockThreshold,
    string? Description,
    bool IsActive,
    int Version,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int CreatedByUserId,
    int? UpdatedByUserId);
