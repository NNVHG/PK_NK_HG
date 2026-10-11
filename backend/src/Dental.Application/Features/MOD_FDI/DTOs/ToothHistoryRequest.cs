namespace Dental.Application.Features.MOD_FDI.DTOs;
public sealed record ToothHistoryRequest(int PatientId, int? ToothNumber = null, int Page = 1, int PageSize = 20);
public sealed record ToothHistoryServiceResponse(int AssignmentId, string Code, string Name, int Quantity, decimal UnitPrice, string? Surface);
public sealed record ToothHistoryItemResponse(int Id, string EventType, int VisitId, DateTime VisitDate, DateTime CreatedAt,
    string? DentistName, int? ToothNumber, string? ConditionCode, string? Surface, string? Note,
    IReadOnlyList<ToothHistoryServiceResponse> Services);
