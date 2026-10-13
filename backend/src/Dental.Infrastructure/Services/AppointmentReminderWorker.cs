using Dental.Application.Features.Appointments.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Services;

public sealed class AppointmentReminderWorker(IServiceScopeFactory scopes, TimeProvider time,
    AppointmentReminderWorkerStatus status, ILogger<AppointmentReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(AppointmentReminderWorkerStatus.IntervalMinutes), time);
        try
        {
            do
            {
                stoppingToken.ThrowIfCancellationRequested();
                status.Start(time.GetUtcNow().UtcDateTime);
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<AppointmentReminderService>();
                    var result = await service.ScanAutomaticallyAsync(stoppingToken);
                    status.Complete(result.IsSuccess ? "Success" : "Conflict", time.GetUtcNow().UtcDateTime, result.Value);
                    if (result.IsSuccess) logger.LogInformation("MOD_APP automatic reminder scan completed. CreatedCount={CreatedCount}", result.Value!.CreatedCount);
                    else logger.LogWarning("MOD_APP automatic reminder scan conflicted. Code={Code}", result.Error.Code);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    status.Complete("Failed", time.GetUtcNow().UtcDateTime);
                    // Never log exception contents: database errors may include patient details.
                    logger.LogWarning("MOD_APP automatic reminder scan failed. Waiting for the next scheduled scan.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { status.Stop(); }
    }
}
