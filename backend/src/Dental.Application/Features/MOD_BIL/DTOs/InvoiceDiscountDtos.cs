namespace Dental.Application.Features.MOD_BIL.DTOs;

public sealed record DiscountPresetRequest(string Code, string Name, string DiscountType, decimal Value, bool IsActive);
public sealed record DiscountPresetResponse(int Id, string Code, string Name, string DiscountType, decimal Value, bool IsActive);
public sealed record ApplyInvoiceDiscountRequest(string Mode, int? PresetId, string? DiscountType, decimal? Value,
    string Reason, decimal ExpectedTotalAmount, decimal ExpectedCurrentDiscountAmount, decimal ExpectedDiscountAmount);
public sealed record InvoiceDiscountResponse(int InvoiceId, decimal TotalAmount, decimal DiscountAmount,
    decimal FinalAmount, decimal RemainingAmount);
