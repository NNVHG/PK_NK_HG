namespace Dental.Application.Features.MOD_FDI.DTOs;

public sealed record LatestToothConditionResponse(
    int Id,
    int ToothNumber,
    string? Surface,
    string ConditionCode,
    DateTime CreatedAt);
