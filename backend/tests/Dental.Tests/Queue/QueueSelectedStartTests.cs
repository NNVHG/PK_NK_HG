using Dental.Application.Common;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Features.Queue.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Tests.Common;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Queue;

public sealed class QueueSelectedStartTests
{
    private readonly IQueueRepository queues = Substitute.For<IQueueRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IVisitRepository visits = Substitute.For<IVisitRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 14, 2, 0, 0, TimeSpan.Zero)));
    private readonly QueueEntry row;
    public QueueSelectedStartTests()
    {
        row = new() { QueueEntryId = 7, VisitId = 8, QueueDate = clock.Today, Status = QueueStatus.Waiting,
            Visit = new Visit { VisitId = 8, Status = VisitStatuses.Created } };
        queues.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(row);
        users.FindByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new User { UserId = 5, IsActive = true, Role = new Role { RoleCode = RoleCodes.Dentist } });
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>()(CancellationToken.None));
    }
    private QueueService Service() => new(queues, Substitute.For<IAppointmentRepository>(), Substitute.For<IPatientRepository>(),
        visits, users, uow, audit, clock, Substitute.For<IInvoiceDraftGenerator>());
    private Task<Result<QueueEntryResponse>> Start(string role = RoleCodes.Dentist, int? dentistId = 5, int id = 7)
        => Service().UpdateQueueStatusAsync(id, 5, role, new(2, dentistId), null);
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)]
    public async Task StartAssignsQueueVisitAndRecordsActor(string role)
    {
        Assert.True((await Start(role)).IsSuccess); Assert.Equal(QueueStatus.InConsultation, row.Status);
        Assert.Equal(5, row.DentistId); Assert.Equal(5, row.Visit!.DentistId); Assert.Equal(VisitStatuses.InProgress, row.Visit.Status); Assert.NotNull(row.Visit.StartedAt);
        await queues.Received(1).AddStatusHistoryAsync(Arg.Is<QueueStatusHistory>(h => h.ChangedByUserId == 5 && h.ToStatus == QueueStatus.InConsultation), Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync(AuditActions.QueueStatusChanged, "QueueEntry", 7, 5, Arg.Is<string>(s => s.Contains("StartSelected")), null, Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)]
    public async Task ForbiddenRolesCannotReadOrWrite(string role)
    { Assert.Equal(Error.QueueTransitionForbidden, (await Start(role)).Error); Assert.Empty(uow.ReceivedCalls()); Assert.Empty(queues.ReceivedCalls()); }
    [Fact] public async Task DentistCannotSpoofAnotherDentist()
    { Assert.Equal(Error.QueueTransitionForbidden, (await Start(dentistId: 6)).Error); Assert.Empty(uow.ReceivedCalls()); }
    [Fact] public async Task DentistDefaultsToJWTActor()
    { Assert.True((await Start(dentistId: null)).IsSuccess); Assert.Equal(5, row.DentistId); }
    [Fact] public async Task AdminNeedsDentistWhenUnassigned()
    { Assert.Equal(Error.QueueDentistRequired, (await Start(RoleCodes.Admin, null)).Error); Assert.Empty(audit.ReceivedCalls()); }
    [Fact] public async Task AnotherActiveVisitBlocksSelectedStart()
    {
        visits.HasActiveVisitForDentistAsync(5, Arg.Any<CancellationToken>(), 8).Returns(true);
        Assert.Equal(Error.DentistHasActiveVisit, (await Start()).Error); Assert.Equal(QueueStatus.Waiting, row.Status); Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData("OLD_DAY")] [InlineData("MISSING_VISIT")] [InlineData("LOCKED")] [InlineData("COMPLETED_VISIT")]
    [InlineData("FOREIGN_QUEUE")] [InlineData("FOREIGN_VISIT")] [InlineData("INACTIVE_DENTIST")] [InlineData("WRONG_ROLE")]
    public async Task InvalidOrForeignCandidateNeverMutates(string state)
    {
        switch (state)
        {
            case "OLD_DAY": row.QueueDate = clock.Today.AddDays(-1); break;
            case "MISSING_VISIT": row.Visit = null; break;
            case "LOCKED": row.Visit!.IsLocked = true; break;
            case "COMPLETED_VISIT": row.Visit!.Status = VisitStatuses.Completed; break;
            case "FOREIGN_QUEUE": row.DentistId = 6; break;
            case "FOREIGN_VISIT": row.Visit!.DentistId = 6; break;
            default: users.FindByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new User { IsActive = state != "INACTIVE_DENTIST", Role = new Role { RoleCode = RoleCodes.Receptionist } }); break;
        }
        Assert.True((await Start()).IsFailure); Assert.Equal(QueueStatus.Waiting, row.Status); Assert.Empty(audit.ReceivedCalls());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
    [Fact] public async Task CancelledEntryCannotStart()
    { row.Status = QueueStatus.Cancelled; Assert.True((await Start()).IsFailure); Assert.Empty(audit.ReceivedCalls()); }
    [Fact] public async Task MissingEntryReturns404()
    { Assert.Equal(Error.QueueEntryNotFound, (await Start(id: 99)).Error); }
    [Fact] public async Task ReturnFromImagingKeepsOriginalStartedAt()
    {
        var started = DateTime.UtcNow.AddHours(-1); row.Visit!.Status = VisitStatuses.InProgress; row.Visit.DentistId = 5; row.Visit.StartedAt = started;
        Assert.True((await Start()).IsSuccess); Assert.Equal(started, row.Visit.StartedAt);
        await visits.Received().HasActiveVisitForDentistAsync(5, Arg.Any<CancellationToken>(), 8);
    }
    [Fact] public async Task SerializationConflictReturns409ErrorWithoutRetry()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<QueueEntryResponse>>(new ConcurrentOperationException()));
        Assert.Equal(Error.QueueConcurrent, (await Start()).Error); Assert.Single(uow.ReceivedCalls());
    }
}
