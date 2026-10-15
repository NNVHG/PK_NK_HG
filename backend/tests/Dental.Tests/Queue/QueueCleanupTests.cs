using Dental.Application.Common;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Features.Queue.Services;
using Dental.Application.Features.Queue.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Tests.Common;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Queue;

public sealed class QueueCleanupTests
{
    private readonly IQueueRepository queues = Substitute.For<IQueueRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 16, 16, 30, 0, TimeSpan.Zero)));
    private QueueCleanupService Service() => new(queues, uow, audit, clock);
    public QueueCleanupTests()
    {
        queues.GetExpiredUnstartedAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<QueueEntry>());
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueCleanupResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<QueueCleanupResponse>>>>()(CancellationToken.None));
    }
    private static QueueEntry Entry(QueueStatus status = QueueStatus.Waiting) => new() { QueueEntryId = 7, VisitId = 8,
        QueueDate = new(2026, 10, 16), Status = status, Visit = new() { VisitId = 8, Status = VisitStatuses.Created } };

    [Theory] [InlineData(23, 29, 15)] [InlineData(23, 30, 16)] [InlineData(23, 59, 16)] [InlineData(0, 0, 15)] [InlineData(12, 0, 15)]
    public void CutoffUsesVietnamDayAt2330(int hour, int minute, int expiredDay)
        => Assert.Equal(new DateOnly(2026, 10, expiredDay), QueueExpiryRules.LastExpiredDate(new(2026, 10, 16, hour, minute, 0)));
    [Theory] [InlineData(QueueStatus.Waiting)] [InlineData(QueueStatus.Calling)] [InlineData(QueueStatus.Skipped)]
    public async Task AutomaticCleanupKeepsHistoryWithSystemActor(QueueStatus status)
    {
        var entry = Entry(status); queues.GetExpiredUnstartedAsync(new(2026, 10, 16), Arg.Any<CancellationToken>()).Returns(new[] { entry });
        var result = await Service().RunAutomaticallyAsync(); Assert.True(result.IsSuccess); Assert.Equal(1, result.Value!.CancelledCount);
        Assert.Equal(QueueStatus.Cancelled, entry.Status); Assert.Equal(VisitStatuses.Cancelled, entry.Visit!.Status); Assert.Equal(clock.UtcNow, entry.Visit.EndedAt);
        await queues.Received(1).AddStatusHistoryAsync(Arg.Is<QueueStatusHistory>(h => h.FromStatus == status && h.ToStatus == QueueStatus.Cancelled && h.ChangedByUserId == null), Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync(AuditActions.QueueStatusChanged, "QueueEntry", 7, null,
            Arg.Is<string>(s => s.Contains("Automatic") && s.Contains("ExpireUnstarted")), null, Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)]
    public async Task ManualCleanupRecordsActualActor(string role)
    {
        var entry = Entry(); queues.GetExpiredUnstartedAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new[] { entry });
        Assert.True((await Service().RunManuallyAsync(99, role, true)).IsSuccess);
        await queues.Received(1).AddStatusHistoryAsync(Arg.Is<QueueStatusHistory>(h => h.ChangedByUserId == 99), Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync(AuditActions.QueueStatusChanged, "QueueEntry", 7, 99, Arg.Is<string>(s => s.Contains("Manual")), null, Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task OtherRolesCannotTriggerCleanup(string? role)
    { Assert.Equal(Error.Forbidden, (await Service().RunManuallyAsync(99, role, true)).Error); Assert.Empty(queues.ReceivedCalls()); Assert.Empty(uow.ReceivedCalls()); }
    [Fact] public async Task ConfirmationAndPositiveActorAreRequired()
    {
        Assert.Equal(Error.Validation, (await Service().RunManuallyAsync(99, RoleCodes.Admin, false)).Error);
        Assert.Equal(Error.Validation, (await Service().RunManuallyAsync(0, RoleCodes.Admin, true)).Error);
        Assert.False(new QueueCleanupRequestValidator().Validate(new QueueCleanupRequest(false)).IsValid);
        Assert.Empty(queues.ReceivedCalls());
    }
    [Theory] [InlineData(QueueStatus.Completed)] [InlineData(QueueStatus.Cancelled)] [InlineData(QueueStatus.InConsultation)] [InlineData(QueueStatus.InImaging)]
    public async Task OtherQueueStatusesAreNeverClosed(QueueStatus status)
    {
        var entry = Entry(status); queues.GetExpiredUnstartedAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new[] { entry });
        Assert.Equal(0, (await Service().RunAutomaticallyAsync()).Value!.CancelledCount); Assert.Equal(status, entry.Status); Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData(VisitStatuses.InProgress)] [InlineData(VisitStatuses.Completed)] [InlineData(VisitStatuses.Cancelled)]
    public async Task StartedAndClosedVisitsAreNeverClosed(string status)
    {
        var entry = Entry(); entry.Visit!.Status = status; queues.GetExpiredUnstartedAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new[] { entry });
        Assert.Equal(0, (await Service().RunAutomaticallyAsync()).Value!.CancelledCount); Assert.Equal(status, entry.Visit.Status); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public void StartedTimestampLockedMissingVisitOrFutureDayAreNotExpired()
    {
        var entry = Entry(); var cutoff = new DateOnly(2026, 10, 16);
        entry.Visit!.StartedAt = clock.UtcNow; Assert.False(QueueExpiryRules.IsExpired(entry, cutoff));
        entry.Visit.StartedAt = null; entry.Visit.IsLocked = true; Assert.False(QueueExpiryRules.IsExpired(entry, cutoff));
        entry.Visit.IsLocked = false; entry.QueueDate = cutoff.AddDays(1); Assert.False(QueueExpiryRules.IsExpired(entry, cutoff));
        entry.QueueDate = cutoff; entry.Visit = null; Assert.False(QueueExpiryRules.IsExpired(entry, cutoff));
    }
    [Fact] public void ManualCleanupEndpointRequiresReceptionPolicy()
    {
        var endpoint = typeof(Dental.Api.Controllers.QueueController).GetMethod(nameof(Dental.Api.Controllers.QueueController.CleanupExpired))!;
        Assert.Contains(endpoint.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(), a => a.Policy == Dental.Api.Policies.Policies.QueueCheckIn);
    }
    [Fact] public async Task EmptyScanIsHonestZeroAndNeverAudits()
    { Assert.Equal(0, (await Service().RunAutomaticallyAsync()).Value!.CancelledCount); Assert.Empty(audit.ReceivedCalls()); }
    [Fact] public async Task ConflictReturnsReloadError()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueCleanupResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns<Result<QueueCleanupResponse>>(_ => throw new ConcurrentOperationException());
        Assert.Equal(Error.QueueConcurrent, (await Service().RunAutomaticallyAsync()).Error);
    }
    [Fact] public async Task RequiredAuditFailurePropagatesToRollbackBoundary()
    {
        queues.GetExpiredUnstartedAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new[] { Entry() });
        audit.LogRequiredAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Synthetic audit failure")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().RunAutomaticallyAsync());
    }
}
