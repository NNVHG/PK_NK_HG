namespace Dental.Application.Features.MOD_BIL.DTOs;

public sealed record ReceiptRequest(int InvoiceId, int PaymentId);
public sealed record ReceiptLineDto(string Name, int Quantity, decimal UnitPrice, decimal TotalAmount);
public sealed record ReceiptPrintDto(int InvoiceId, string InvoiceCode, int PaymentId, string ClinicName,
    string? ClinicAddress, string? ClinicPhone, string PatientName, string PatientCode, int BirthYear,
    string CashierName, DateTime PaidAt, string PaymentMethod, string? Source, decimal Amount,
    decimal? AmountTendered, decimal? ChangeAmount, string? Note, decimal FinalAmount, decimal PaidAmount,
    decimal RemainingAmount, int PrintCount, int PrintNumber, DateTime? PrintedAt, IReadOnlyList<ReceiptLineDto> Items);
