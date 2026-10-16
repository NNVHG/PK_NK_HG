namespace Dental.Application.Features.MOD_MST.DTOs;
public sealed record DiagnosisQuery(string? Keyword = null, bool IncludeInactive = false, int Page = 1, int PageSize = 20);
public sealed record DiagnosisWriteRequest(string Code, string Name, string? Description, string Category, bool IsActive = true, int ExpectedVersion = 0);
public sealed record DiagnosisResponse(int Id, string Code, string Name, string? Description, string Category, bool IsActive, int Version);
