using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;

namespace Dental.Application.Features.Queue.Services;

public sealed class QueueCleanupService(IQueueRepository queues, IUnitOfWork uow, IAuditLogger audit, VietnamClock clock)
{
    public Task<Result<QueueCleanupResponse>> RunAutomaticallyAsync(CancellationToken ct = default)
        => CleanupAsync(null, ct);

    public Task<Result<QueueCleanupResponse>> RunManuallyAsync(int actorId, string? role, bool confirmed,
        CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist))
            return Task.FromResult(Result<QueueCleanupResponse>.Failure(Error.Forbidden));
        if (actorId <= 0 || !confirmed) return Task.FromResult(Result<QueueCleanupResponse>.Failure(Error.Validation));
        return CleanupAsync(actorId, ct);
    }

    private async Task<Result<QueueCleanupResponse>> CleanupAsync(int? actorId, CancellationToken ct)
    {
        try
        {
            return await uow.ExecuteSerializableTransactionAsync(async transactionCt =>
            {
                var cutoff = QueueExpiryRules.LastExpiredDate(clock.Now);
                var candidates = await queues.GetExpiredUnstartedAsync(cutoff, transactionCt);
                var count = 0;
                foreach (var entry in candidates)
                {
                    if (!QueueExpiryRules.IsExpired(entry, cutoff)) continue;
                    var history = QueueExpiryRules.Cancel(entry, actorId, clock.UtcNow);
                    await queues.AddStatusHistoryAsync(history, transactionCt);
                    await uow.SaveChangesAsync(transactionCt);
                    await audit.LogRequiredAsync(AuditActions.QueueStatusChanged, "QueueEntry", entry.QueueEntryId, actorId,
                        JsonSerializer.Serialize(new { action = "ExpireUnstarted", fromStatus = (int?)history.FromStatus,
                            toStatus = (int)history.ToStatus, visitId = entry.VisitId, source = actorId.HasValue ? "Manual" : "Automatic" }), ct: transactionCt);
                    count++;
                }
                return Result<QueueCleanupResponse>.Success(new(count, cutoff));
            }, ct);
        }
        catch (ConcurrentOperationException) { return Result<QueueCleanupResponse>.Failure(Error.QueueConcurrent); }
    }
}
