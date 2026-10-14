namespace Dental.Application.Features.MOD_BIL.DTOs;
public sealed class InvoiceLookupRequest { public string? Code { get; set; } }
public sealed record InvoiceLookupResponse(int InvoiceId, string InvoiceCode, int Status, decimal TotalAmount,
    decimal PaidAmount, decimal RemainingAmount, string PatientCode, string PatientName, DateTime CreatedAt);
