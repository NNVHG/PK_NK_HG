using System.Text.Json;
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

public sealed class QueueLocationTests
{
    private readonly IQueueRepository queues = Substitute.For<IQueueRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 16, 2, 0, 0, TimeSpan.Zero)));
    private QueueService Service() => new(queues, Substitute.For<IAppointmentRepository>(), Substitute.For<IPatientRepository>(),
        Substitute.For<IVisitRepository>(), users, uow, Substitute.For<IAuditLogger>(), clock, Substitute.For<IInvoiceDraftGenerator>());
    private QueueEntry Row(int id, int? dentist = 5, QueueStatus status = QueueStatus.Calling) => new()
    {
        QueueEntryId = id, DentistId = dentist, QueueDate = clock.Today, Status = status, QueueNumber = id,
        Patient = new() { FullName = "Synthetic Middle Patient", Phone = "private-phone", PatientId = 77 },
        Notes = "private-note", Visit = new() { Diagnosis = "private-diagnosis" }
    };
    public QueueLocationTests()
    {
        queues.GetDentistLocationsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new QueueDentistLocation(5, "G03", "Phòng 3") });
    }

    [Fact] public async Task PublicMonitorUsesDestinationLocationAndKeepsMinimalWhitelist()
    {
        queues.GetPublicDisplayQueueAsync(clock.Today, Arg.Any<CancellationToken>()).Returns(new[] { Row(1), Row(2, 5, QueueStatus.Waiting) });
        var result = await Service().GetPublicQueueDisplayAsync();
        foreach (var item in result.Value!.InConsultation.Concat(result.Value.Waiting))
        {
            Assert.Equal("Phòng 3", item.RoomName); Assert.Equal("G03", item.ChairNumber);
            Assert.NotEqual("Synthetic Middle Patient", item.MaskedPatientName);
            var names = JsonSerializer.SerializeToElement(item).EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
            Assert.Equal(new[] { "ChairNumber", "DentistName", "IsPriority", "MaskedPatientName", "QueueEntryId", "QueueNumber", "QueueNumberDisplay", "RoomName", "Status", "StatusText" }, names);
        }
        await queues.Received(1).GetDentistLocationsAsync(Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 5 })), Arg.Any<CancellationToken>());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory] [InlineData(null)] [InlineData(9)]
    public async Task MissingOrUnknownDestinationNeverBorrowsAnotherDentistsRoom(int? dentist)
    {
        queues.GetPublicDisplayQueueAsync(clock.Today, Arg.Any<CancellationToken>()).Returns(new[] { Row(1, dentist) });
        var item = Assert.Single((await Service().GetPublicQueueDisplayAsync()).Value!.InConsultation);
        Assert.Null(item.RoomName); Assert.Null(item.ChairNumber);
        if (!dentist.HasValue) await queues.DidNotReceive().GetDentistLocationsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact] public async Task InternalPageReadsLocationsOnceForDistinctDentists()
    {
        queues.GetTodayQueueAsync(clock.Today, null, null, 1, 20, Arg.Any<CancellationToken>())
            .Returns((new[] { Row(1), Row(2), Row(3, null) }, 3));
        var result = await Service().GetTodayQueueAsync(new());
        Assert.Equal("Phòng 3", result.Value!.Items[0].RoomName);
        Assert.Equal("G03", result.Value.Items[1].ChairNumber); Assert.Null(result.Value.Items[2].RoomName);
        await queues.Received(1).GetDentistLocationsAsync(Arg.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(5)), Arg.Any<CancellationToken>());
    }

    [Fact] public async Task DetailReturnsActualLocationWithoutAddingWorkspaceIdentifiers()
    {
        queues.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(Row(1));
        var item = (await Service().GetQueueEntryDetailAsync(1)).Value!.Entry;
        Assert.Equal("Phòng 3", item.RoomName); Assert.Equal("G03", item.ChairNumber);
    }

    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)]
    public async Task CallingDirectoryAddsOnlyLocationWithoutChangingPermittedDentists(string role)
    {
        users.GetActiveDentistsAsync(Arg.Any<CancellationToken>()).Returns(new[] { new User { UserId = 5, FullName = "Doctor A" }, new User { UserId = 9, FullName = "Doctor B" } });
        var result = await Service().GetCallingDentistsAsync(role);
        Assert.Equal(new[] { 5, 9 }, result.Value!.Select(d => d.DentistId));
        Assert.Equal("Phòng 3", result.Value![0].RoomName); Assert.Null(result.Value![1].RoomName);
    }
}
