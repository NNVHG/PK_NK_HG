using System.Collections.Concurrent;
using Dental.Application.Common;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Features.Queue.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Queue;

public sealed class QueueCleanupWorkerTests
{
    private readonly IQueueRepository queues = Substitute.For<IQueueRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly SteppedTime time = new();
    private readonly ScanLogger logger = new();
    public QueueCleanupWorkerTests()
    {
        queues.GetExpiredUnstartedAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(_ => new[] { new QueueEntry {
            QueueEntryId = 7, QueueDate = new(2026, 10, 15), Status = Dental.Domain.Enums.QueueStatus.Waiting,
            Visit = new Visit { Status = VisitStatuses.Created } } });
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueCleanupResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<QueueCleanupResponse>>>>()(c.Arg<CancellationToken>()));
    }
    private ServiceProvider Services() => new ServiceCollection().AddSingleton(queues).AddSingleton(uow).AddSingleton(audit)
        .AddSingleton(new VietnamClock(time)).AddScoped<QueueCleanupService>().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    private QueueCleanupWorker Worker(IServiceProvider services) => new(services.GetRequiredService<IServiceScopeFactory>(), time, logger);
    [Fact] public async Task StartupAndMinuteTicksScanSequentiallyAndStopDisposesTimer()
    {
        await using var services = Services(); using var worker = Worker(services);
        await worker.StartAsync(CancellationToken.None); await logger.Next();
        Assert.Equal(TimeSpan.FromMinutes(1), time.Period);
        await queues.Received(1).GetExpiredUnstartedAsync(new(2026, 10, 15), Arg.Any<CancellationToken>());
        time.Tick(); await logger.Next(); await queues.Received(2).GetExpiredUnstartedAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await worker.StopAsync(CancellationToken.None); Assert.True(time.Disposed);
    }
    [Fact] public async Task FailureDoesNotLogSensitiveExceptionAndNextTickCanRecover()
    {
        queues.GetExpiredUnstartedAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns<IReadOnlyList<QueueEntry>>(_ => throw new InvalidOperationException("sensitive patient"));
        await using var services = Services(); using var worker = Worker(services);
        await worker.StartAsync(CancellationToken.None); await logger.Next(); Assert.Contains(logger.Lines, l => l.Contains("failed"));
        Assert.All(logger.Lines, l => Assert.DoesNotContain("sensitive patient", l)); Assert.False(logger.SawException);
        queues.GetExpiredUnstartedAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new[] { new QueueEntry {
            QueueDate = new(2026, 10, 15), Visit = new Visit { Status = VisitStatuses.Created } } });
        time.Tick(); await logger.Next(); Assert.Contains(logger.Lines, l => l.Contains("completed")); await worker.StopAsync(CancellationToken.None);
    }
    [Fact] public async Task ConflictWaitsForNextTickWithoutImmediateRetry()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueCleanupResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<QueueCleanupResponse>>(new ConcurrentOperationException()));
        await using var services = Services(); using var worker = Worker(services);
        await worker.StartAsync(CancellationToken.None); await logger.Next(); Assert.Single(uow.ReceivedCalls());
        Assert.Contains(logger.Lines, l => l.Contains("CHK_010")); await worker.StopAsync(CancellationToken.None);
    }
    [Fact] public async Task SlowScanDoesNotOverlapAndStopCancelsPendingQuery()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        async Task<IReadOnlyList<QueueEntry>> Block(NSubstitute.Core.CallInfo c)
        { Interlocked.Increment(ref calls); entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, c.Arg<CancellationToken>()); return Array.Empty<QueueEntry>(); }
        queues.GetExpiredUnstartedAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(c => Block(c));
        await using var services = Services(); using var worker = Worker(services);
        await worker.StartAsync(CancellationToken.None); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); time.Tick(); time.Tick(); Assert.Equal(1, calls);
        await worker.StopAsync(CancellationToken.None); Assert.True(time.Disposed); Assert.DoesNotContain(logger.Lines, l => l.Contains("failed"));
    }
    private sealed class ScanLogger : ILogger<QueueCleanupWorker>
    {
        private readonly SemaphoreSlim events = new(0); public readonly ConcurrentQueue<string> Lines = new(); public bool SawException;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> format)
        { if (exception is not null) SawException = true; Lines.Enqueue(format(state, exception)); events.Release(); }
        public async Task Next() => Assert.True(await events.WaitAsync(TimeSpan.FromSeconds(5)));
    }
    private sealed class SteppedTime : TimeProvider
    {
        private TestTimer? timer; public TimeSpan Period; public bool Disposed => timer?.Disposed == true;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 16, 2, 0, 0, TimeSpan.Zero);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { Period = period; timer = new(callback, state); return timer; }
        public void Tick() => timer!.Fire();
        private sealed class TestTimer(TimerCallback callback, object? state) : ITimer
        {
            public bool Disposed; public void Fire() { if (!Disposed) callback(state); }
            public bool Change(TimeSpan dueTime, TimeSpan period) => !Disposed;
            public void Dispose() => Disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
