using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Appointments.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Appointments;

public sealed class PatientSelfReschedulingTests
{
    private readonly IAppointmentRepository appointments = Substitute.For<IAppointmentRepository>();
    private readonly IPatientRepository patients = Substitute.For<IPatientRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly MutableClock time = new();
    private readonly VietnamClock clock;
    private readonly Appointment row;
    public PatientSelfReschedulingTests()
    {
        clock = new(time);
        row = new() { AppointmentId = 7, PatientId = 9, AppointmentDate = clock.Today.AddDays(1), SlotTime = new(9, 0), Status = AppointmentStatuses.Scheduled, Notes = "Original note" };
        appointments.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(row);
        patients.GetByIdAsync(9, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 9, UserId = 10 });
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<AppointmentResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<AppointmentResponse>>>>()(CancellationToken.None));
    }
    private AppointmentService Service() => new(appointments, patients, users, uow, audit, clock);
    private RescheduleAppointmentRequest Request() => new(clock.Today.AddDays(3), new(10, 0), Reason: "Synthetic shift");
    private Task<Result<AppointmentResponse>> Move(RescheduleAppointmentRequest? request = null, int actor = 10, string role = RoleCodes.Patient)
        => Service().RescheduleAppointmentAsync(7, actor, role, request ?? Request(), null);
    private void OriginalTime(double minutes)
    {
        var at = clock.Now.AddMinutes(minutes);
        row.AppointmentDate = DateOnly.FromDateTime(at); row.SlotTime = TimeOnly.FromDateTime(at);
    }
    [Fact] public async Task OwnerCanMoveInPlaceAndKeepsStaffNotes()
    {
        var result = await Move(); Assert.True(result.IsSuccess); Assert.Equal(7, result.Value!.AppointmentId);
        Assert.Equal(1, row.RescheduleCount); Assert.Equal("Original note", row.Notes);
        await audit.Received(1).LogAsync(AuditActions.AppointmentRescheduled, "Appointment", 7, 10, Arg.Any<string>(), null, Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(11)] [InlineData(0)]
    public async Task ForeignActorCannotReadOrMove(int actor)
    {
        Assert.Equal(Error.AppointmentNotFound, (await Service().GetSelfReschedulingEligibilityAsync(7, actor)).Error);
        Assert.Equal(Error.AppointmentNotFound, (await Move(actor: actor)).Error); Assert.Equal(0, row.RescheduleCount); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task MissingAndUnlinkedProfilesAreHidden()
    {
        Assert.Equal(Error.AppointmentNotFound, (await Service().GetSelfReschedulingEligibilityAsync(999, 10)).Error);
        patients.GetByIdAsync(9, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 9 });
        Assert.Equal(Error.AppointmentNotFound, (await Move()).Error);
    }
    [Theory] [InlineData(120, true)] [InlineData(119, false)] [InlineData(-1, false)]
    public async Task OriginalAppointmentUsesExact120MinuteBoundary(int minutes, bool allowed)
    {
        OriginalTime(minutes);
        var eligibility = await Service().GetSelfReschedulingEligibilityAsync(7, 10);
        Assert.Equal(allowed, eligibility.Value!.CanReschedule);
        Assert.Equal(allowed, (await Move()).IsSuccess);
        if (!allowed) { Assert.Contains("Lễ tân", eligibility.Value.Message); Assert.Equal(0, row.RescheduleCount); }
    }
    [Fact] public async Task EligibilityIsReadOnlyAndRecheckedAtWriteTime()
    {
        OriginalTime(120); Assert.True((await Service().GetSelfReschedulingEligibilityAsync(7, 10)).Value!.CanReschedule);
        Assert.Empty(audit.ReceivedCalls()); Assert.Empty(uow.ReceivedCalls());
        time.UtcNow = time.UtcNow.AddMinutes(1); Assert.True((await Move()).IsFailure); Assert.Equal(0, row.RescheduleCount);
    }
    [Theory] [InlineData(0, true, 2)] [InlineData(1, true, 1)] [InlineData(2, false, 0)] [InlineData(3, false, 0)]
    public async Task LimitsTotalReschedulesOnCurrentAppointment(int count, bool allowed, int remaining)
    {
        row.RescheduleCount = count; var eligibility = await Service().GetSelfReschedulingEligibilityAsync(7, 10);
        Assert.Equal(allowed, eligibility.Value!.CanReschedule); Assert.Equal(remaining, eligibility.Value.RemainingChanges);
        Assert.Equal(allowed, (await Move()).IsSuccess); Assert.Equal(count + (allowed ? 1 : 0), row.RescheduleCount);
    }
    [Theory] [InlineData(AppointmentStatuses.CheckedIn)] [InlineData(AppointmentStatuses.Cancelled)] [InlineData(AppointmentStatuses.Completed)]
    public async Task OnlyScheduledMayMove(string state)
    {
        row.Status = state; Assert.False((await Service().GetSelfReschedulingEligibilityAsync(7, 10)).Value!.CanReschedule);
        Assert.True((await Move()).IsFailure); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task PatientCannotChangeDentistOrStaffNotes()
    {
        Assert.Equal(Error.Forbidden, (await Move(Request() with { DentistId = 30 })).Error);
        Assert.Equal(Error.Forbidden, (await Move(Request() with { Notes = "Spoofed" })).Error);
        Assert.Equal(0, row.RescheduleCount); Assert.Equal("Original note", row.Notes);
    }
    [Fact] public async Task RetainsAssignedDentistAndRejectsTheirBusySlot()
    {
        row.DentistId = 30;
        users.FindByIdAsync(30, Arg.Any<CancellationToken>()).Returns(new User { UserId = 30, IsActive = true, Role = new Role { RoleCode = RoleCodes.Dentist } });
        appointments.HasDentistBookingAsync(30, Request().AppointmentDate, Request().SlotTime, 7, Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(Error.Validation.Code, (await Move()).Error.Code); Assert.Equal(0, row.RescheduleCount);
    }
    [Fact] public async Task PatientReplayAtLimitDoesNotConsumeAnotherChange()
    {
        row.RescheduleCount = 2; OriginalTime(120); time.UtcNow = time.UtcNow.AddMinutes(1);
        Assert.True((await Move(new(row.AppointmentDate, row.SlotTime, Reason: "Replay"))).IsSuccess);
        Assert.Equal(2, row.RescheduleCount); Assert.Empty(audit.ReceivedCalls()); await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)]
    public async Task StaffMayHelpAfterPatientLimitAndCutoff(string role)
    {
        row.RescheduleCount = 2; OriginalTime(119); Assert.True((await Move(role: role)).IsSuccess); Assert.Equal(3, row.RescheduleCount);
    }
    [Fact] public async Task SerializationConflictReturnsReloadGuidance()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<AppointmentResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<AppointmentResponse>>(new ConcurrentOperationException()));
        Assert.Equal(Error.AppointmentConcurrent, (await Move()).Error);
    }
    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
