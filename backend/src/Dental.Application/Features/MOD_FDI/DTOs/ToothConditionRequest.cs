namespace Dental.Application.Features.MOD_FDI.DTOs;

public sealed record ToothConditionRequest(int ToothNumber, string? Surface, string ConditionCode);

public sealed record ToothConditionResponse(int Id, int VisitId, int ToothNumber, string? Surface,
    string ConditionCode, string? Note, DateTime CreatedAt);
