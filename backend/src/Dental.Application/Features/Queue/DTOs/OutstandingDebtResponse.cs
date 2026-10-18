namespace Dental.Application.Features.Queue.DTOs;

public sealed record OutstandingDebtResponse(
    decimal TotalOutstandingDebt,
    IReadOnlyList<string> InvoiceCodes);
