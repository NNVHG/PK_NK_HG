namespace Dental.Application.Features.MOD_FDI.DTOs;

/// <summary>Danh sách răng null/rỗng là dịch vụ toàn hàm; Surface áp dụng cho mọi răng trong batch.</summary>
public sealed record AssignServicesRequest(int ServiceId, int[]? ToothNumbers, string? Surface, int Quantity = 1);

public sealed record AssignedServiceResponse(int Id, int VisitId, int ServiceId, int ServicePriceId,
    string ServiceCode, string ServiceName, int? ToothNumber, string? Surface, int Quantity,
    decimal UnitPrice, decimal TotalAmount, DateTime CreatedAt);
