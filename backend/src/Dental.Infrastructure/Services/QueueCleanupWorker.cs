using Dental.Application.Features.Queue.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Services;

/// <summary>Runs at startup and every minute, including catch-up after downtime.</summary>
public sealed class QueueCleanupWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<QueueCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<QueueCleanupService>().RunAutomaticallyAsync(stoppingToken);
                    if (result.IsFailure) logger.LogWarning("MOD_CHK expiry scan conflicted. Code={Code}", result.Error.Code);
                    else if (result.Value!.CancelledCount > 0) logger.LogInformation("MOD_CHK expiry scan completed. Count={Count}", result.Value.CancelledCount);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception) { logger.LogWarning("MOD_CHK expiry scan failed; will retry on the next tick."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
