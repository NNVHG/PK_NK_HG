using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IBankTransferRepository
{
    Task<Invoice?> GetInvoiceAsync(int invoiceId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetBankSettingsAsync(CancellationToken ct = default);
    Task<Result<PaymentTransaction>> ReceiveAsync(BankTransferRequest request, string invoiceCode, int actorId,
        bool simulated, string? ip, CancellationToken ct = default);
}
