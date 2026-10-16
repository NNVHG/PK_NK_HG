using Dental.Api.Controllers;
using Dental.Application.Common;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Features.Queue.Services;
using Dental.Application.Features.Queue.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Tests.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Tests.Queue;

public sealed class QueueCallingTests
{
    private readonly IQueueRepository queues = Substitute.For<IQueueRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IVisitRepository visits = Substitute.For<IVisitRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 16, 2, 0, 0, TimeSpan.Zero)));
    private readonly QueueEntry row;
    private QueueService Service() => new(queues, Substitute.For<IAppointmentRepository>(), Substitute.For<IPatientRepository>(),
        visits, users, uow, audit, clock, Substitute.For<IInvoiceDraftGenerator>(), Dental.Tests.Common.TestVisitChairs.Create());
    public QueueCallingTests()
    {
        row = new() { QueueEntryId = 7, VisitId = 8, DentistId = 5, QueueDate = clock.Today, Status = QueueStatus.Waiting,
            Visit = new Visit { VisitId = 8, Status = VisitStatuses.Created } };
        users.FindByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new User { UserId = 5, IsActive = true, Role = new Role { RoleCode = RoleCodes.Dentist } });
        users.GetActiveDentistsAsync(Arg.Any<CancellationToken>()).Returns(new[] { new User { UserId = 5, FullName = "Synthetic dentist" } });
        queues.GetNextWaitingEntryAsync(clock.Today, 5, Arg.Any<CancellationToken>(), Arg.Any<bool>()).Returns(row);
        queues.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(row);
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>()(CancellationToken.None));
    }
    private Task<Result<QueueEntryResponse>> Call(string role = RoleCodes.Assistant)
        => Service().CallNextAsync(role == RoleCodes.Dentist ? 5 : 99, role, new(role == RoleCodes.Dentist ? null : 5), null);

    [Fact] public void ExistingPersistedStatusValuesRemainUnchanged()
    {
        Assert.Equal(1, (int)QueueStatus.Waiting); Assert.Equal(2, (int)QueueStatus.InConsultation);
        Assert.Equal(3, (int)QueueStatus.Completed); Assert.Equal(4, (int)QueueStatus.Cancelled);
        Assert.Equal(5, (int)QueueStatus.InImaging); Assert.Equal(6, (int)QueueStatus.Calling);
        Assert.Equal("Đang mời vào phòng", QueueStatusDescriptions.GetDescription(QueueStatus.Calling));
    }

    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)]
    public async Task AuthorizedCallInvitesWithoutStartingOrAssigningClinicalVisit(string role)
    {
        var result = await Call(role); Assert.True(result.IsSuccess); Assert.Equal((int)QueueStatus.Calling, result.Value!.Status);
        Assert.Equal(VisitStatuses.Created, row.Visit!.Status); Assert.Null(row.Visit.StartedAt); Assert.Null(row.Visit.DentistId);
        Assert.Equal(5, row.DentistId);
        await queues.Received(1).GetNextWaitingEntryAsync(clock.Today, 5, Arg.Any<CancellationToken>(), role == RoleCodes.Assistant);
        await queues.Received(1).AddStatusHistoryAsync(Arg.Is<QueueStatusHistory>(h => h.FromStatus == QueueStatus.Waiting && h.ToStatus == QueueStatus.Calling), Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync(AuditActions.QueueStatusChanged, "QueueEntry", 7, role == RoleCodes.Dentist ? 5 : 99,
            Arg.Is<string>(s => s.Contains("CallNext")), null, Arg.Any<CancellationToken>());
    }
    [Fact] public async Task AssistantCannotAssignAnUnassignedEntryEvenIfRepositoryReturnsOne()
    {
        row.DentistId = null; Assert.Equal(Error.QueueTransitionForbidden, (await Call()).Error);
        Assert.Equal(QueueStatus.Waiting, row.Status); Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Patient)]
    public async Task UnpermittedRolesCannotReadOrCall(string role)
    {
        Assert.Equal(Error.QueueTransitionForbidden, (await Call(role)).Error);
        Assert.Empty(queues.ReceivedCalls()); Assert.Empty(uow.ReceivedCalls());
    }
    [Fact] public async Task DentistCannotSpoofCallingForAnotherDentist()
    {
        Assert.Equal(Error.QueueTransitionForbidden, (await Service().CallNextAsync(5, RoleCodes.Dentist, new(6), null)).Error);
        Assert.Empty(queues.ReceivedCalls());
    }
    [Fact] public async Task ASecondInvitationRequiresResolvingCurrentCall()
    {
        queues.HasCallingForDentistAsync(clock.Today, 5, Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(Error.QueueDentistAlreadyCalling, (await Call()).Error); Assert.Equal(QueueStatus.Waiting, row.Status);
        await queues.DidNotReceive().GetNextWaitingEntryAsync(Arg.Any<DateOnly>(), Arg.Any<int?>(), Arg.Any<CancellationToken>(), Arg.Any<bool>());
        Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)]
    public async Task OnlyClinicalStartTransitionsCallingToInProgress(string role)
    {
        row.Status = QueueStatus.Calling;
        var result = await Service().UpdateQueueStatusAsync(7, role == RoleCodes.Dentist ? 5 : 99, role, new(2, 5), null);
        Assert.True(result.IsSuccess); Assert.Equal(QueueStatus.InConsultation, row.Status);
        Assert.Equal(VisitStatuses.InProgress, row.Visit!.Status); Assert.Equal(5, row.Visit.DentistId); Assert.NotNull(row.Visit.StartedAt);
        await queues.Received(1).AddStatusHistoryAsync(Arg.Is<QueueStatusHistory>(h => h.FromStatus == QueueStatus.Calling && h.ToStatus == QueueStatus.InConsultation), Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Patient)]
    public async Task CallPermissionCannotBeUsedToStartClinicalVisit(string role)
    {
        row.Status = QueueStatus.Calling;
        Assert.Equal(Error.QueueTransitionForbidden, (await Service().UpdateQueueStatusAsync(7, 99, role, new(2, 5), null)).Error);
        Assert.Equal(VisitStatuses.Created, row.Visit!.Status); Assert.Null(row.Visit.StartedAt); Assert.Empty(queues.ReceivedCalls());
    }
    [Fact] public async Task CannotStartADifferentWaitingPatientWhileAnotherCallIsPending()
    {
        queues.HasCallingForDentistAsync(clock.Today, 5, Arg.Any<CancellationToken>(), 7).Returns(true);
        Assert.Equal(Error.QueueDentistAlreadyCalling, (await Service().UpdateQueueStatusAsync(7, 5, RoleCodes.Dentist, new(2), null)).Error);
        Assert.Equal(VisitStatuses.Created, row.Visit!.Status); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task StatusUpdateCannotBypassCallNextGuards()
    {
        Assert.Equal(Error.Validation, (await Service().UpdateQueueStatusAsync(7, 99, RoleCodes.Assistant, new(6), null)).Error);
        Assert.False(new UpdateQueueStatusRequestValidator().Validate(new UpdateQueueStatusRequest(6)).IsValid);
        Assert.Empty(queues.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)]
    public async Task CallingDirectoryIsMinimalAndAuthorized(string role)
    {
        var result = await Service().GetCallingDentistsAsync(role); Assert.True(result.IsSuccess);
        var dentist = Assert.Single(result.Value!); Assert.Equal(5, dentist.DentistId); Assert.Equal("Synthetic dentist", dentist.DentistName);
        Assert.Equal(new[] { "ChairNumber", "DentistId", "DentistName", "RoomName" },
            dentist.GetType().GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Null(dentist.ChairNumber); Assert.Null(dentist.RoomName);
    }
    [Theory] [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Receptionist)] [InlineData(null)]
    public async Task CallingDirectoryRejectsOtherRolesBeforeReading(string? role)
    { Assert.Equal(Error.Forbidden, (await Service().GetCallingDentistsAsync(role)).Error); Assert.Empty(users.ReceivedCalls()); }
    [Fact] public void CallingEndpointsUseDedicatedPolicyAndNoStoreForDirectory()
    {
        foreach (var name in new[] { nameof(QueueController.CallNext), nameof(QueueController.GetCallingDentists) })
        {
            var method = typeof(QueueController).GetMethod(name)!;
            Assert.Contains(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), p => p.Policy == AppPolicies.QueueCall);
        }
        Assert.True(((ResponseCacheAttribute)Attribute.GetCustomAttribute(typeof(QueueController).GetMethod(nameof(QueueController.GetCallingDentists))!, typeof(ResponseCacheAttribute))!).NoStore);
    }
    [Fact] public async Task PublicMonitorIncludesCallingFirstWithoutClinicalData()
    {
        row.Status = QueueStatus.Calling;
        var examining = new QueueEntry { QueueEntryId = 9, Status = QueueStatus.InConsultation, UpdatedAt = DateTime.UtcNow.AddDays(1) };
        queues.GetPublicDisplayQueueAsync(clock.Today, Arg.Any<CancellationToken>()).Returns(new[] { examining, row });
        var result = (await Service().GetPublicQueueDisplayAsync()).Value!;
        Assert.Equal(7, result.InConsultation[0].QueueEntryId); Assert.Equal(6, result.InConsultation[0].Status);
        Assert.Empty(result.Waiting);
    }
}
