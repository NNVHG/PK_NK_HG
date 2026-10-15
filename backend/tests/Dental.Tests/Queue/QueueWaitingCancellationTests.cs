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

public sealed class QueueWaitingCancellationTests
{
    private readonly IQueueRepository queues = Substitute.For<IQueueRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 14, 2, 0, 0, TimeSpan.Zero)));
    private QueueService Service() => new(queues, Substitute.For<IAppointmentRepository>(), Substitute.For<IPatientRepository>(),
        Substitute.For<IVisitRepository>(), Substitute.For<IInvoiceRepository>(), Substitute.For<IUserRepository>(), uow, audit, clock, Substitute.For<IInvoiceDraftGenerator>());
    public QueueWaitingCancellationTests()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>()(CancellationToken.None));
    }
    private QueueEntry Waiting() => new() { QueueEntryId = 7, VisitId = 8, QueueDate = clock.Today, Status = QueueStatus.Waiting,
        Visit = new Visit { VisitId = 8, Status = VisitStatuses.Created } };
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)]
    public async Task CancelWritesQueueVisitHistoryAndAuditWithoutSensitiveReason(string role)
    {
        var row = Waiting(); queues.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(row);
        var result = await Service().CancelWaitingAsync(7, 5, role, new(true, "  Synthetic reason  "), null);
        Assert.True(result.IsSuccess); Assert.Equal(QueueStatus.Cancelled, row.Status);
        Assert.Equal(VisitStatuses.Cancelled, row.Visit!.Status); Assert.NotNull(row.Visit.EndedAt); Assert.Null(row.Visit.StartedAt);
        await queues.Received(1).AddStatusHistoryAsync(Arg.Is<QueueStatusHistory>(h => h.ChangedByUserId == 5 &&
            h.FromStatus == QueueStatus.Waiting && h.ToStatus == QueueStatus.Cancelled && h.Reason == "Synthetic reason"), Arg.Any<CancellationToken>());
        await audit.Received(1).LogAsync(AuditActions.QueueStatusChanged, "QueueEntry", 7, 5,
            Arg.Is<string>(d => d.Contains("CancelWaiting") && !d.Contains("Synthetic reason")), null, Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)]
    public async Task ForbiddenRoleCannotReadOrWrite(string role)
    {
        Assert.Equal(Error.QueueTransitionForbidden, (await Service().CancelWaitingAsync(7, 5, role, new(true), null)).Error);
        Assert.Empty(queues.ReceivedCalls()); Assert.Empty(uow.ReceivedCalls());
    }
    [Theory] [InlineData("CALLED")] [InlineData("COMPLETED")] [InlineData("CANCELLED")]
    [InlineData("OLD_DAY")] [InlineData("MISSING_VISIT")] [InlineData("STARTED")]
    [InlineData("IMAGING_RETURN")] [InlineData("LOCKED")]
    public async Task StaleOrClinicalQueueCannotBeCancelled(string state)
    {
        var row = Waiting();
        switch (state)
        {
            case "CALLED": row.Status = QueueStatus.InConsultation; break;
            case "COMPLETED": row.Status = QueueStatus.Completed; break;
            case "CANCELLED": row.Status = QueueStatus.Cancelled; break;
            case "OLD_DAY": row.QueueDate = clock.Today.AddDays(-1); break;
            case "MISSING_VISIT": row.Visit = null; break;
            case "STARTED": row.Visit!.StartedAt = DateTime.UtcNow; break;
            case "IMAGING_RETURN": row.Visit!.Status = VisitStatuses.InProgress; break;
            case "LOCKED": row.Visit!.IsLocked = true; break;
        }
        var before = row.Status; queues.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(row);
        Assert.Equal(Error.QueueWaitingCancellationBlocked, (await Service().CancelWaitingAsync(7, 5, RoleCodes.Admin, new(true), null)).Error);
        Assert.Equal(before, row.Status); Assert.Empty(audit.ReceivedCalls());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task MissingQueueReturnsNotFound()
        => Assert.Equal(Error.QueueEntryNotFound, (await Service().CancelWaitingAsync(7, 5, RoleCodes.Admin, new(true), null)).Error);
    [Fact]
    public async Task ConcurrentConflictReturnsErrorWithoutRetry()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<QueueEntryResponse>>(new ConcurrentOperationException()));
        Assert.Equal(Error.QueueWaitingCancellationConcurrent, (await Service().CancelWaitingAsync(7, 5, RoleCodes.Admin, new(true), null)).Error);
        Assert.Single(uow.ReceivedCalls());
    }
    [Theory] [InlineData(0, true, 0)] [InlineData(7, false, 0)] [InlineData(7, true, 501)]
    public async Task ServiceRejectsInvalidInput(int id, bool confirmed, int length)
    {
        Assert.Equal(Error.Validation, (await Service().CancelWaitingAsync(id, 5, RoleCodes.Admin, new(confirmed, new string('x', length)), null)).Error);
        Assert.Empty(queues.ReceivedCalls()); Assert.Empty(uow.ReceivedCalls());
    }
    [Theory] [InlineData(false, 0, false)] [InlineData(true, 500, true)] [InlineData(true, 501, false)]
    public void ConfirmationAndReasonBoundaries(bool confirmed, int length, bool valid)
        => Assert.Equal(valid, new CancelWaitingRequestValidator().Validate(new CancelWaitingRequest(confirmed, new string('x', length))).IsValid);
}
