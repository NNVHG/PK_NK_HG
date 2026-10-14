namespace Dental.Application.Features.MOD_BIL.DTOs;

public sealed record InvoicePortalItemResponse(
    int ItemId, string Code, string Name, int Quantity, decimal UnitPrice, decimal TotalAmount);
