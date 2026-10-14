namespace Dental.Application.Features.MOD_BIL.DTOs;
public class InvoicePortalQuery
{
    public string? PatientId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
public sealed record InvoicePortalPaymentResponse(int PaymentId, int InvoiceId, decimal Amount, string PaymentMethod,
    DateTime PaidAt, bool IsSimulation);
