using System.Collections.Concurrent;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Appointments.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Appointments;

public sealed class AppointmentReminderWorkerTests
{
    private readonly IAppointmentReminderRepository repository = Substitute.For<IAppointmentReminderRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly SteppedTime time = new();
    private readonly AppointmentReminderWorkerStatus status = new();
    private readonly ScanLogger logger = new();
    private int createdScopes, disposedScopes;
    public AppointmentReminderWorkerTests()
    {
        repository.GetCandidatesAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Appointment>());
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<SimulateAppointmentRemindersResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<SimulateAppointmentRemindersResponse>>>>()(call.Arg<CancellationToken>()));
    }
    private ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new Probe(() => Interlocked.Increment(ref disposedScopes)));
        services.AddScoped(sp => { sp.GetRequiredService<Probe>(); Interlocked.Increment(ref createdScopes); return new AppointmentReminderService(repository, uow, audit, new VietnamClock(time), time); });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
    private AppointmentReminderWorker Worker(ServiceProvider services) => new(services.GetRequiredService<IServiceScopeFactory>(), time, status, logger);
    private async Task WaitForScopeDisposal(int expected)
    {
        // Logging occurs just before async scope disposal, so wait for the probe's event loop turn.
        for (var n = 0; n < 100 && Volatile.Read(ref disposedScopes) < expected; n++) await Task.Delay(5);
        Assert.Equal(expected, Volatile.Read(ref disposedScopes));
    }
    [Fact] public async Task StartsImmediatelyThenUses30MinuteTimerAndFreshScopes()
    {
        await using var services = Services(); using var worker = Worker(services);
        await worker.StartAsync(CancellationToken.None); await logger.Next(); await WaitForScopeDisposal(1);
        Assert.Equal("Success", status.GetStatus().State); Assert.Equal(TimeSpan.FromMinutes(30), time.Period);
        Assert.Equal(TimeSpan.FromMinutes(30), time.DueTime); Assert.Equal(1, createdScopes);
        time.Tick(); await logger.Next(); await WaitForScopeDisposal(2); Assert.Equal(2, createdScopes);
        await worker.StopAsync(CancellationToken.None); Assert.Equal("Stopped", status.GetStatus().State); Assert.True(time.Disposed);
    }
    [Fact] public async Task UnexpectedFailureDoesNotExposeExceptionOrStopFutureScans()
    {
        repository.GetCandidatesAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<Appointment>>(new InvalidOperationException("Synthetic sensitive patient details")));
        await using var services = Services(); using var worker = Worker(services); await worker.StartAsync(CancellationToken.None); await logger.Next();
        Assert.Equal("Failed", status.GetStatus().State); Assert.Null(status.GetStatus().CreatedCount);
        Assert.All(logger.Lines, line => Assert.DoesNotContain("sensitive patient", line)); Assert.False(logger.SawException);
        repository.GetCandidatesAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Appointment>());
        time.Tick(); await logger.Next(); Assert.Equal("Success", status.GetStatus().State); await worker.StopAsync(CancellationToken.None);
    }
    [Fact] public async Task ConflictIsReportedWithoutImmediateRetry()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<SimulateAppointmentRemindersResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<SimulateAppointmentRemindersResponse>>(new ConcurrentOperationException()));
        await using var services = Services(); using var worker = Worker(services); await worker.StartAsync(CancellationToken.None); await logger.Next();
        Assert.Equal("Conflict", status.GetStatus().State); Assert.Single(uow.ReceivedCalls());
        Assert.Contains(logger.Lines, line => line.Contains("APP_010")); await worker.StopAsync(CancellationToken.None);
    }
    [Fact] public async Task SlowScanCannotOverlapAndCancellationIsPropagated()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        async Task<IReadOnlyList<Appointment>> Block(NSubstitute.Core.CallInfo call)
        { Interlocked.Increment(ref calls); entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>()); return Array.Empty<Appointment>(); }
        repository.GetCandidatesAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(call => Block(call));
        await using var services = Services(); using var worker = Worker(services); await worker.StartAsync(CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); time.Tick(); time.Tick(); Assert.Equal(1, calls);
        await worker.StopAsync(CancellationToken.None); Assert.Equal("Stopped", status.GetStatus().State); Assert.Equal(1, disposedScopes);
        Assert.DoesNotContain(logger.Lines, line => line.Contains("failed"));
    }
    [Fact] public void StatusResetOnNewProcessContainsNoPreviousResult()
    {
        var before = status.GetStatus(); Assert.Equal("NotStarted", before.State); Assert.Equal(30, before.IntervalMinutes); Assert.Null(before.LastCompletedAtUtc);
        status.Start(time.GetUtcNow().UtcDateTime); status.Complete("Success", time.GetUtcNow().UtcDateTime, new(3, 1, 2, default, default));
        Assert.Equal(1, status.GetStatus().CreatedCount); Assert.Null(before.CreatedCount); Assert.Null(new AppointmentReminderWorkerStatus().GetStatus().CreatedCount);
    }
    private sealed class Probe(Action dispose) : IAsyncDisposable { public ValueTask DisposeAsync() { dispose(); return ValueTask.CompletedTask; } }
    private sealed class ScanLogger : ILogger<AppointmentReminderWorker>
    {
        private readonly SemaphoreSlim events = new(0); public readonly ConcurrentQueue<string> Lines = new(); public bool SawException;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> format)
        { if (exception is not null) SawException = true; Lines.Enqueue(format(state, exception)); events.Release(); }
        public async Task Next() => Assert.True(await events.WaitAsync(TimeSpan.FromSeconds(5)), "Expected a worker scan completion event.");
    }
    private sealed class SteppedTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 13, 2, 0, 0, TimeSpan.Zero); private TestTimer? timer;
        public TimeSpan DueTime, Period; public bool Disposed => timer?.Disposed == true;
        public override DateTimeOffset GetUtcNow() => now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { DueTime = dueTime; Period = period; timer = new(callback, state); return timer; }
        public void Tick() { now += TimeSpan.FromMinutes(30); timer!.Fire(); }
        private sealed class TestTimer(TimerCallback callback, object? state) : ITimer
        {
            public bool Disposed; public void Fire() { if (!Disposed) callback(state); }
            public bool Change(TimeSpan dueTime, TimeSpan period) => !Disposed;
            public void Dispose() => Disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
