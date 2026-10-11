namespace Dental.Application.Features.MOD_BIL.DTOs;

public sealed record InvoiceItemResponse(string ItemType, string Code, string Name, int? ToothNumber,
    string? Surface, int Quantity, decimal UnitPrice, decimal TotalAmount);
public sealed record InvoiceResponse(int Id, int VisitId, string InvoiceCode, int Status,
    decimal TotalAmount, decimal PaidAmount, decimal RemainingAmount, DateTime CreatedAt,
    IReadOnlyList<InvoiceItemResponse> Items);
