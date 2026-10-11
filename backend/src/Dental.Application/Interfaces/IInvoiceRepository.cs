using Dental.Application.Common;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IInvoiceRepository
{
    Task<Invoice?> GetForVisitAsync(int visitId, CancellationToken ct = default);
    Task<int?> GetQueueIdAsync(int visitId, CancellationToken ct = default);
    Task<Result<InvoiceDraftSynchronization>> SynchronizeDraftAsync(int visitId, int userId, DateOnly date, CancellationToken ct = default);
    // Saves pending tracked Visit/Queue/history changes with the invoice in one serializable transaction.
    Task<Result<Invoice>> SaveCompletedVisitDraftAsync(Visit visit, int userId, DateOnly date, CancellationToken ct = default);
}

public sealed record InvoiceDraftSynchronization(Invoice Invoice, bool Changed);

public interface IInvoiceDraftGenerator
{
    Task<Result> GenerateAsync(Visit completedVisit, int userId, string role, string? ip, CancellationToken ct = default);
}
