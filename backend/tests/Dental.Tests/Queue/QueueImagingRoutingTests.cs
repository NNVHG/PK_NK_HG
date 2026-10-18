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

public sealed class QueueImagingRoutingTests
{
    private readonly IQueueRepository queues = Substitute.For<IQueueRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 16, 2, 0, 0, TimeSpan.Zero)));
    private readonly QueueEntry row;
    private QueueService Service() => new(queues, Substitute.For<IAppointmentRepository>(), Substitute.For<IPatientRepository>(),
        Substitute.For<IVisitRepository>(), Substitute.For<IInvoiceRepository>(), users, uow, audit, clock, Substitute.For<IInvoiceDraftGenerator>(), Dental.Tests.Common.TestVisitChairs.Create());
    public QueueImagingRoutingTests()
    {
        row = new() { QueueEntryId = 7, VisitId = 8, DentistId = 5, QueueDate = clock.Today, Status = QueueStatus.InConsultation,
            Visit = new Visit { VisitId = 8, DentistId = 5, Status = VisitStatuses.InProgress, StartedAt = DateTime.UtcNow.AddMinutes(-20) } };
        queues.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(row);
        users.FindByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new User { UserId = 5, IsActive = true, Role = new Role { RoleCode = RoleCodes.Dentist } });
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>()(CancellationToken.None));
    }
    private Task<Result<QueueEntryResponse>> Route(int target = 5, string role = RoleCodes.Dentist, int actor = 5, string? reason = "  Synthetic indication  ", int? dentistId = null)
        => Service().UpdateQueueStatusAsync(7, actor, role, new(target, dentistId, reason), null);

    [Theory] [InlineData(RoleCodes.Dentist, 5)] [InlineData(RoleCodes.Admin, 99)]
    public async Task SendToImaging_KeepsVisitDentistAndPersistsIndicationAtomically(string role, int actor)
    {
        var started = row.Visit!.StartedAt;
        Assert.True((await Route(role: role, actor: actor)).IsSuccess);
        Assert.Equal(QueueStatus.InImaging, row.Status); Assert.Equal(5, row.DentistId); Assert.Equal(5, row.Visit.DentistId);
        Assert.Equal(VisitStatuses.InProgress, row.Visit.Status); Assert.Equal(started, row.Visit.StartedAt); Assert.False(row.Visit.IsLocked);
        await queues.Received(1).AddStatusHistoryAsync(Arg.Is<QueueStatusHistory>(h => h.FromStatus == QueueStatus.InConsultation &&
            h.ToStatus == QueueStatus.InImaging && h.ChangedByUserId == actor && h.Reason == "Synthetic indication"), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync(AuditActions.QueueStatusChanged, "QueueEntry", 7, actor,
            Arg.Is<string>(s => s.Contains("ImagingRouting") && !s.Contains("Synthetic indication")), null, Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Admin)]
    public async Task CompleteImaging_ReturnsToWaitingWithSameOpenVisitAndPriority(string role)
    {
        row.Status = QueueStatus.InImaging; var visit = row.Visit; var started = visit!.StartedAt;
        Assert.True((await Route(1, role, 99, null)).IsSuccess); Assert.Equal(QueueStatus.Waiting, row.Status); Assert.True(row.IsPriority);
        Assert.Same(visit, row.Visit); Assert.Equal(5, visit.DentistId); Assert.Equal(5, row.DentistId); Assert.Equal(started, visit.StartedAt);
        Assert.Equal(VisitStatuses.InProgress, visit.Status); Assert.Null(visit.EndedAt);
    }
    [Theory]
    [InlineData(5, RoleCodes.Receptionist)] [InlineData(5, RoleCodes.Assistant)] [InlineData(5, RoleCodes.Patient)]
    [InlineData(1, RoleCodes.Receptionist)] [InlineData(1, RoleCodes.Dentist)] [InlineData(1, RoleCodes.Patient)]
    public async Task WrongRole_NeverReadsOrWrites(int target, string role)
    {
        var source = target == 5 ? QueueStatus.InConsultation : QueueStatus.InImaging;
        Assert.Equal(QueueStateMachine.ValidateTransition(source, (QueueStatus)target, role).Error, (await Route(target, role)).Error);
        Assert.Empty(queues.ReceivedCalls()); Assert.Empty(uow.ReceivedCalls());
    }
    [Fact] public async Task ForeignDentist_CannotOrderImaging()
    { Assert.Equal(Error.QueueTransitionForbidden, (await Route(actor: 6)).Error); await AssertNoWrite(); }
    [Theory] [InlineData(5)] [InlineData(1)]
    public async Task CannotReassignDentistThroughImaging(int target)
    { Assert.Equal(Error.Validation, (await Route(target, RoleCodes.Admin, dentistId: 6)).Error); Assert.Empty(queues.ReceivedCalls()); }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("   ")]
    public async Task OrderRequiresRecordedIndication(string? reason)
    { Assert.Equal(Error.Validation, (await Route(reason: reason)).Error); await AssertNoWrite(); }
    [Fact] public async Task OversizedReasonIsRejected()
    { Assert.Equal(Error.Validation, (await Route(reason: new string('x', 501))).Error); await AssertNoWrite(); }
    [Theory] [InlineData("OLD_DAY")] [InlineData("MISSING_VISIT")] [InlineData("LOCKED")]
    [InlineData("CREATED")] [InlineData("COMPLETED")] [InlineData("CANCELLED")]
    [InlineData("MISSING_DENTIST")] [InlineData("MISMATCH_DENTIST")] [InlineData("WRONG_QUEUE_STATUS")]
    [InlineData("INACTIVE_DENTIST")] [InlineData("INVALID_DENTIST")]
    public async Task InvalidStateCannotChangeQueueOrHistory(string state)
    {
        switch (state)
        {
            case "OLD_DAY": row.QueueDate = clock.Today.AddDays(-1); break;
            case "MISSING_VISIT": row.Visit = null; break;
            case "LOCKED": row.Visit!.IsLocked = true; break;
            case "CREATED": row.Visit!.Status = VisitStatuses.Created; break;
            case "COMPLETED": row.Visit!.Status = VisitStatuses.Completed; break;
            case "CANCELLED": row.Visit!.Status = VisitStatuses.Cancelled; break;
            case "MISSING_DENTIST": row.DentistId = null; break;
            case "MISMATCH_DENTIST": row.Visit!.DentistId = 6; break;
            case "WRONG_QUEUE_STATUS": row.Status = QueueStatus.Waiting; break;
            default: users.FindByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new User { IsActive = state != "INACTIVE_DENTIST", Role = new Role { RoleCode = RoleCodes.Receptionist } }); break;
        }
        var original = row.Status; Assert.True((await Route()).IsFailure); Assert.Equal(original, row.Status); await AssertNoWrite();
    }
    [Fact] public async Task DuplicateRequestIsRejectedWithoutSecondAudit()
    { Assert.True((await Route()).IsSuccess); Assert.True((await Route()).IsFailure); Assert.Single(audit.ReceivedCalls()); }
    [Fact] public async Task SerializationConflictIsBusinessError()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<QueueEntryResponse>>>(_ => throw new ConcurrentOperationException());
        Assert.Equal(Error.QueueConcurrent, (await Route()).Error); await AssertNoWrite();
    }
    [Fact] public async Task PublicMonitorPrioritizesReturnFromImagingWithoutExposingHistory()
    {
        row.Status = QueueStatus.Waiting; row.IsPriority = true;
        row.StatusHistories.Add(new() { FromStatus = QueueStatus.InImaging, ToStatus = QueueStatus.Waiting, Reason = "Synthetic secret indication" });
        var booked = new QueueEntry { QueueEntryId = 9, Status = QueueStatus.Waiting, IsPriority = true,
            Appointment = new Appointment { SlotTime = new(8, 0) } };
        queues.GetPublicDisplayQueueAsync(clock.Today, Arg.Any<CancellationToken>()).Returns(new[] { booked, row });
        var result = (await Service().GetPublicQueueDisplayAsync()).Value!;
        Assert.Equal(7, result.Waiting[0].QueueEntryId);
        Assert.DoesNotContain("Synthetic secret indication", System.Text.Json.JsonSerializer.Serialize(result));
    }
    [Fact] public void ValidatorRequiresIndicationAndPreservesDentist()
    {
        var validator = new UpdateQueueStatusRequestValidator();
        Assert.False(validator.Validate(new UpdateQueueStatusRequest(5, Reason: "  ")).IsValid);
        Assert.False(validator.Validate(new UpdateQueueStatusRequest(1, DentistId: 6)).IsValid);
        Assert.True(validator.Validate(new UpdateQueueStatusRequest(5, Reason: new string('x', 500))).IsValid);
        Assert.True(validator.Validate(new UpdateQueueStatusRequest(1)).IsValid);
    }
    private async Task AssertNoWrite()
    { await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>()); Assert.Empty(audit.ReceivedCalls());
        await queues.DidNotReceive().AddStatusHistoryAsync(Arg.Any<QueueStatusHistory>(), Arg.Any<CancellationToken>()); }
}
