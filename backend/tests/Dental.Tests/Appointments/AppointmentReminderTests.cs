using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Appointments.Services;
using Dental.Application.Features.Appointments.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Tests.Common;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Appointments;

public sealed class AppointmentReminderTests
{
    private readonly IAppointmentReminderRepository repository = Substitute.For<IAppointmentReminderRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly TimeProvider time = new FixedTimeProvider(new(2026, 10, 13, 2, 0, 0, TimeSpan.Zero));
    private readonly VietnamClock clock;
    public AppointmentReminderTests()
    {
        clock = new(time);
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<SimulateAppointmentRemindersResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<SimulateAppointmentRemindersResponse>>>>()(CancellationToken.None));
        repository.GetCandidatesAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Appointment>());
    }
    private AppointmentReminderService Service() => new(repository, uow, audit, clock, time);
    private Appointment Row(double hours = 24, double lead = 48, string state = AppointmentStatuses.Scheduled)
    {
        var at = clock.Now.AddHours(hours);
        return new() { AppointmentId = 7, PatientId = 9, Status = state, AppointmentDate = DateOnly.FromDateTime(at), SlotTime = TimeOnly.FromDateTime(at),
            CreatedAt = time.GetUtcNow().UtcDateTime.AddHours(hours - lead), Patient = new Patient { FullName = "Synthetic patient", Phone = "0999999931" }, Dentist = new User { FullName = "Synthetic dentist" } };
    }
    private void Candidates(params Appointment[] rows) => repository.GetCandidatesAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(rows);
    private Task<Result<SimulateAppointmentRemindersResponse>> Run(string? role = RoleCodes.Receptionist, bool confirmed = true) => Service().SimulateAsync(10, role, confirmed, null);
    [Fact] public async Task AutomaticScanUsesSystemActorAndExplicitSource()
    {
        Candidates(Row()); Assert.Equal(1, (await Service().ScanAutomaticallyAsync()).Value!.CreatedCount);
        var args = audit.ReceivedCalls().Single().GetArguments(); Assert.Null(args[3]); Assert.Null(args[5]);
        using var detail = JsonDocument.Parse((string)args[4]!); Assert.Equal("Automatic", detail.RootElement.GetProperty("source").GetString());
    }
    [Fact] public async Task ManualScanPreservesHumanActorAndExplicitSource()
    {
        Candidates(Row()); await Run(); var args = audit.ReceivedCalls().Single().GetArguments(); Assert.Equal(10, args[3]);
        using var detail = JsonDocument.Parse((string)args[4]!); Assert.Equal("Manual", detail.RootElement.GetProperty("source").GetString());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)]
    public async Task StaffCreatesVietnameseSnapshotWithoutAuditPII(string role)
    {
        Candidates(Row()); var result = await Run(role); Assert.Equal(1, result.Value!.CreatedCount);
        var log = (NotificationLog)repository.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(repository.AddAsync)).GetArguments()[0]!;
        Assert.Equal("SMS_SIMULATED", log.Channel); Assert.Equal("Sent", log.Status); Assert.Equal(time.GetUtcNow().UtcDateTime, log.SentAtUtc);
        Assert.Contains("09:00 14/10/2026", log.MessageContent); Assert.Contains("BS Synthetic dentist", log.MessageContent); Assert.Contains("0901234567", log.MessageContent);
        var detail = (string)audit.ReceivedCalls().Single().GetArguments()[4]!; Assert.DoesNotContain("Synthetic patient", detail); Assert.DoesNotContain(log.RecipientPhone, detail);
    }
    [Theory] [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)] [InlineData(null)]
    public async Task DeniedRolesNeverReadOrWrite(string? role)
    { Assert.Equal(Error.Forbidden, (await Run(role)).Error); Assert.Empty(repository.ReceivedCalls()); Assert.Empty(uow.ReceivedCalls()); }
    [Fact] public async Task ConfirmationRequiredBeforeTransaction()
    { Assert.Equal(Error.Validation, (await Run(confirmed: false)).Error); Assert.Empty(uow.ReceivedCalls()); }
    [Theory] [InlineData(23.5, 1)] [InlineData(24.5, 1)] [InlineData(23.49, 0)] [InlineData(24.51, 0)]
    public async Task WindowUsesInclusiveBoundaries(double hours, int expected)
    { Candidates(Row(hours)); Assert.Equal(expected, (await Run()).Value!.CreatedCount); }
    [Theory] [InlineData(24, 1)] [InlineData(23.99, 0)]
    public async Task UrgentBookingUnder24HoursIsSkipped(double lead, int expected)
    { Candidates(Row(lead: lead)); Assert.Equal(expected, (await Run()).Value!.EligibleCount); }
    [Theory] [InlineData(AppointmentStatuses.Cancelled)] [InlineData(AppointmentStatuses.CheckedIn)] [InlineData(AppointmentStatuses.Completed)]
    public async Task OnlyScheduledIsReminded(string state)
    { Candidates(Row(state: state)); Assert.Equal(0, (await Run()).Value!.CreatedCount); Assert.Empty(audit.ReceivedCalls()); }
    [Fact] public async Task ExactOccurrenceReplayNeverWritesOrAudits()
    {
        var row = Row(); Candidates(row); repository.HasOccurrenceAsync(7, row.AppointmentDate, row.SlotTime, Arg.Any<CancellationToken>()).Returns(true);
        var result = await Run(); Assert.Equal(1, result.Value!.AlreadyRemindedCount); Assert.Equal(0, result.Value.CreatedCount);
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>()); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task SameIdNewSlotIsDistinctAndNoDentistUsesExplicitFallback()
    {
        var row = Row(24.5); row.Dentist = null; Candidates(row); Assert.Equal(1, (await Run()).Value!.CreatedCount);
        await repository.Received().HasOccurrenceAsync(7, row.AppointmentDate, row.SlotTime, Arg.Any<CancellationToken>());
        var log = (NotificationLog)repository.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(repository.AddAsync)).GetArguments()[0]!;
        Assert.Contains("nha sĩ được phân công", log.MessageContent);
    }
    [Fact] public async Task EmptyScanReportsZeroWithoutAudit()
    { Assert.Equal(0, (await Run()).Value!.EligibleCount); Assert.Empty(audit.ReceivedCalls()); }
    [Fact] public async Task ConflictIsReturnedWithoutAutomaticRetry()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<SimulateAppointmentRemindersResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<SimulateAppointmentRemindersResponse>>(new ConcurrentOperationException()));
        Assert.Equal(Error.AppointmentReminderConcurrent, (await Run()).Error);
        Assert.Single(uow.ReceivedCalls());
    }
    [Fact] public async Task ReadLogsIsPagedAndOmitsRecipientPhone()
    {
        repository.GetLogsAsync(7, 2, 20, Arg.Any<CancellationToken>()).Returns((new[] { new NotificationLog { RecipientPhone = "0999999931", MessageContent = "Synthetic message" } }, 21));
        var result = await Service().GetLogsAsync(RoleCodes.Receptionist, new(7, 2)); Assert.Equal(21, result.Value!.TotalCount); Assert.Equal(2, result.Value.Page);
        Assert.DoesNotContain("RecipientPhone", JsonSerializer.Serialize(result.Value)); Assert.DoesNotContain("0999999931", JsonSerializer.Serialize(result.Value)); Assert.Empty(audit.ReceivedCalls()); Assert.Empty(uow.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Dentist)]
    public async Task DeniedRolesCannotReadMessageContent(string role)
    { Assert.Equal(Error.Forbidden, (await Service().GetLogsAsync(role, new())).Error); Assert.Empty(repository.ReceivedCalls()); }
    [Theory] [InlineData(0, 1, 20, false)] [InlineData(null, 0, 20, false)] [InlineData(null, 1, 101, false)] [InlineData(7, 1000000, 100, true)]
    public void ValidatorBoundsQuery(int? id, int page, int size, bool valid) => Assert.Equal(valid, new ReminderLogQueryValidator().Validate(new ReminderLogQueryRequest(id, page, size)).IsValid);
    [Fact] public void ValidatorRequiresExplicitTrue() => Assert.False(new SimulateAppointmentRemindersValidator().Validate(new SimulateAppointmentRemindersRequest(false)).IsValid);
    [Theory] [InlineData(EntityState.Modified)] [InlineData(EntityState.Deleted)]
    public async Task NotificationLogIsAppendOnly(EntityState state)
    {
        await using var db = new DentalDbContext(new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql("Host=unused;Database=unused;Username=unused").Options);
        db.Entry(new NotificationLog { Id = 1 }).State = state;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [Fact] public void ModelIncludesFKAndOccurrenceKey()
    {
        using var db = new DentalDbContext(new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql("Host=unused;Database=unused;Username=unused").Options);
        var entity = db.Model.FindEntityType(typeof(NotificationLog))!; Assert.Equal(2, entity.GetForeignKeys().Count());
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.GetDatabaseName() == "IX_NotificationLogs_ReminderOccurrence");
    }
}
