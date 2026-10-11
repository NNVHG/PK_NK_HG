namespace Dental.Application.Features.MOD_BIL.DTOs;

public sealed record CashPaymentRequest(decimal Amount, decimal AmountTendered, Guid RequestId, string PaymentMethod = "Cash");
public sealed record CashPaymentResponse(int Id, int InvoiceId, decimal Amount, decimal? AmountTendered,
    decimal? ChangeAmount, string PaymentMethod, int CashierId, DateTime PaidAt, Guid RequestId);
