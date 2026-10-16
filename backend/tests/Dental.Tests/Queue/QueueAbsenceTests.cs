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

public sealed class QueueAbsenceTests
{
    private readonly IQueueRepository queues = Substitute.For<IQueueRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IVisitRepository visits = Substitute.For<IVisitRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 16, 2, 0, 0, TimeSpan.Zero)));
    private readonly QueueEntry row;
    private QueueService Service() => new(queues, Substitute.For<IAppointmentRepository>(), Substitute.For<IPatientRepository>(),
        visits, users, uow, audit, clock, Substitute.For<IInvoiceDraftGenerator>());
    public QueueAbsenceTests()
    {
        row = new() { QueueEntryId = 7, VisitId = 8, DentistId = 5, QueueDate = clock.Today, Status = QueueStatus.Calling,
            Patient = new Patient { FullName = "Synthetic patient", Phone = "0999999999" },
            Visit = new Visit { VisitId = 8, Status = VisitStatuses.Created } };
        users.FindByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new User { UserId = 5, IsActive = true, Role = new Role { RoleCode = RoleCodes.Dentist } });
        queues.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(row);
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>()(CancellationToken.None));
    }
    private Task<Result<QueueEntryResponse>> Change(string role = RoleCodes.Receptionist, bool recall = false, QueueAbsenceRequest? request = null)
        => Service().ChangeAbsenceAsync(7, role == RoleCodes.Dentist ? 5 : 99, role, recall, request ?? new(true, "  Synthetic note  "), null);

    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Dentist)]
    public async Task SkipKeepsSameOpenVisitAndRecordsRequiredAudit(string role)
    {
        var result = await Change(role); Assert.True(result.IsSuccess); Assert.Equal(7, result.Value!.Status); Assert.False(result.Value.CanMarkAbsent);
        Assert.Equal(5, row.DentistId); Assert.Equal(8, row.VisitId); Assert.Equal(VisitStatuses.Created, row.Visit!.Status); Assert.Null(row.Visit.StartedAt); Assert.Null(row.Visit.EndedAt);
        await queues.Received(1).AddStatusHistoryAsync(Arg.Is<QueueStatusHistory>(h => h.FromStatus == QueueStatus.Calling && h.ToStatus == QueueStatus.Skipped && h.Reason == "Synthetic note"), Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync(AuditActions.QueueStatusChanged, "QueueEntry", 7, role == RoleCodes.Dentist ? 5 : 99,
            Arg.Is<string>(s => s.Contains("MarkAbsent") && !s.Contains("Synthetic") && !s.Contains("0999999999")), null, Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)]
    public async Task RecallRestoresWaitingWithoutInvitingOrStarting(string role)
    {
        row.Status = QueueStatus.Skipped; var result = await Change(role, true);
        Assert.True(result.IsSuccess); Assert.Equal(QueueStatus.Waiting, row.Status); Assert.Equal(5, row.DentistId);
        Assert.Equal(VisitStatuses.Created, row.Visit!.Status); Assert.Null(row.Visit.StartedAt);
        await queues.Received(1).AddStatusHistoryAsync(Arg.Is<QueueStatusHistory>(h => h.FromStatus == QueueStatus.Skipped && h.ToStatus == QueueStatus.Waiting), Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Assistant, false)] [InlineData(RoleCodes.Patient, false)]
    [InlineData(RoleCodes.Dentist, true)] [InlineData(RoleCodes.Assistant, true)] [InlineData(RoleCodes.Patient, true)]
    public async Task DeniedRolesNeverReadOrWrite(string role, bool recall)
    {
        Assert.Equal(Error.QueueTransitionForbidden, (await Change(role, recall)).Error); Assert.Empty(queues.ReceivedCalls()); Assert.Empty(uow.ReceivedCalls());
    }
    [Fact] public async Task DentistCannotSkipAnotherDentistsPatient()
    {
        row.DentistId = 6; Assert.Equal(Error.QueueTransitionForbidden, (await Change(RoleCodes.Dentist)).Error); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task InactiveDentistCannotSkip()
    {
        users.FindByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new User { IsActive = false });
        Assert.Equal(Error.QueueTransitionForbidden, (await Change(RoleCodes.Dentist)).Error);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task MissingVisitAndPastDateCannotChange(bool recall)
    {
        row.Status = recall ? QueueStatus.Skipped : QueueStatus.Calling; row.Visit = null;
        Assert.Equal(Error.QueueAbsenceBlocked, (await Change(recall: recall)).Error);
        row.Visit = new() { Status = VisitStatuses.Created }; row.QueueDate = clock.Today.AddDays(-1);
        Assert.Equal(Error.QueueAbsenceBlocked, (await Change(recall: recall)).Error); Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData(VisitStatuses.InProgress)] [InlineData(VisitStatuses.Completed)] [InlineData(VisitStatuses.Cancelled)]
    public async Task StartedOrClosedClinicalVisitCannotBeSkipped(string status)
    {
        row.Visit!.Status = status; Assert.Equal(Error.QueueAbsenceBlocked, (await Change()).Error);
        Assert.Equal(QueueStatus.Calling, row.Status); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task StartedTimestampLockedOrDifferentClinicalDentistCannotBeSkipped()
    {
        row.Visit!.StartedAt = DateTime.UtcNow; Assert.Equal(Error.QueueAbsenceBlocked, (await Change()).Error);
        row.Visit.StartedAt = null; row.Visit.IsLocked = true; Assert.Equal(Error.QueueAbsenceBlocked, (await Change()).Error);
        row.Visit.IsLocked = false; row.Visit.DentistId = 6; Assert.Equal(Error.QueueAbsenceBlocked, (await Change()).Error);
        Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(7)]
    public async Task SkipRejectsStaleOrUncalledStatus(int status)
    { row.Status = (QueueStatus)status; Assert.Equal(Error.QueueAbsenceBlocked, (await Change()).Error); Assert.Empty(audit.ReceivedCalls()); }
    [Fact] public async Task RecallRequiresSkippedAndUnstartedVisit()
    {
        Assert.Equal(Error.QueueAbsenceBlocked, (await Change(recall: true)).Error);
        row.Status = QueueStatus.Skipped; row.Visit!.Status = VisitStatuses.InProgress;
        Assert.Equal(Error.QueueAbsenceBlocked, (await Change(recall: true)).Error);
    }
    [Fact] public async Task RequiresConfirmationAndLengthLimitAtBothLayers()
    {
        foreach (var request in new[] { new QueueAbsenceRequest(false), new QueueAbsenceRequest(true, new string('x', 501)) })
        { Assert.False(new QueueAbsenceRequestValidator().Validate(request).IsValid); Assert.Equal(Error.Validation, (await Change(request: request)).Error); }
        Assert.Empty(queues.ReceivedCalls()); Assert.True(new QueueAbsenceRequestValidator().Validate(new QueueAbsenceRequest(true, new string('x', 500))).IsValid);
    }
    [Fact] public async Task SerializableConflictReturnsReloadError()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns<Result<QueueEntryResponse>>(_ => throw new ConcurrentOperationException());
        Assert.Equal(Error.QueueConcurrent, (await Change()).Error);
    }
    [Fact] public async Task GenericStatusCannotBypassSkipOrRecallGuards()
    {
        Assert.False(new UpdateQueueStatusRequestValidator().Validate(new UpdateQueueStatusRequest(7)).IsValid);
        Assert.Equal(Error.Validation, (await Service().UpdateQueueStatusAsync(7, 99, RoleCodes.Admin, new(7), null)).Error);
        row.Status = QueueStatus.Skipped;
        Assert.True((await Service().UpdateQueueStatusAsync(7, 99, RoleCodes.Admin, new(1), null)).IsFailure);
        Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public void StateMachineAndPersistedValuesFollowDecision()
    {
        Assert.Equal(5, (int)QueueStatus.InImaging); Assert.Equal(6, (int)QueueStatus.Calling); Assert.Equal(7, (int)QueueStatus.Skipped);
        Assert.True(new QueueQueryRequestValidator().Validate(new QueueQueryRequest(Status: 7)).IsValid);
        Assert.True(QueueStateMachine.ValidateTransition(QueueStatus.Calling, QueueStatus.Skipped, RoleCodes.Receptionist).IsSuccess);
        Assert.True(QueueStateMachine.ValidateTransition(QueueStatus.Skipped, QueueStatus.Waiting, RoleCodes.Receptionist).IsSuccess);
        Assert.True(QueueStateMachine.ValidateTransition(QueueStatus.Skipped, QueueStatus.InConsultation, RoleCodes.Admin).IsFailure);
    }
    [Fact] public async Task EligibilityExcludesStartedVisitButAllowsCreatedInvitation()
    {
        Assert.True((await Service().GetQueueEntryDetailAsync(7)).Value!.Entry.CanMarkAbsent);
        row.Visit!.Status = VisitStatuses.InProgress; Assert.False((await Service().GetQueueEntryDetailAsync(7)).Value!.Entry.CanMarkAbsent);
    }
}
