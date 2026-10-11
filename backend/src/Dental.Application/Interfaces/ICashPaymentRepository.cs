using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface ICashPaymentRepository
{
    Task<Result<PaymentTransaction>> ReceiveAsync(int invoiceId, int cashierId, CashPaymentRequest request, string? ip, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentTransaction>> GetAsync(int invoiceId, CancellationToken ct = default);
    Task<bool> InvoiceExistsAsync(int invoiceId, CancellationToken ct = default);
}
