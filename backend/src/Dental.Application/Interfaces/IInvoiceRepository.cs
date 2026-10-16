using Dental.Application.Common;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IInvoiceRepository
{
    Task<Invoice?> GetForVisitAsync(int visitId, CancellationToken ct = default);
    // Stages cancellation and required audit only; caller must own the Serializable visit transaction.
    Task<Result> StageVisitCancellationAsync(int visitId, int userId, string role, string? ip, CancellationToken ct = default);
    Task<int?> GetQueueIdAsync(int visitId, CancellationToken ct = default);
    Task<Result<InvoiceDraftSynchronization>> SynchronizeDraftAsync(int visitId, int userId, DateOnly date, CancellationToken ct = default);
    // Saves pending tracked Visit/Queue/history changes with the invoice in one serializable transaction.
    Task<Result<Invoice>> SaveCompletedVisitDraftAsync(Visit visit, int userId, DateOnly date, CancellationToken ct = default);
    Task<Result<InvoiceDraftSynchronization>> CompleteCorrectionAsync(int visitId, int userId, string role,
        DateOnly date, string? ip, CancellationToken ct = default);
}

public sealed record InvoiceDraftSynchronization(Invoice Invoice, bool Changed);

public interface IInvoiceDraftGenerator
{
    Task<Result> StageVisitCancellationAsync(Visit visit, int userId, string role, string? ip, CancellationToken ct = default);
    Task<Result> GenerateAsync(Visit completedVisit, int userId, string role, string? ip, CancellationToken ct = default);
}
