using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Appointments.Services;
using Dental.Application.Features.Appointments.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Tests.Common;
using NSubstitute;
using Xunit;
namespace Dental.Tests.Appointments;
public sealed class AppointmentReschedulingTests
{
    private readonly IAppointmentRepository appointments = Substitute.For<IAppointmentRepository>();
    private readonly IPatientRepository patients = Substitute.For<IPatientRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero)));
    public AppointmentReschedulingTests() => uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<AppointmentResponse>>>>(), Arg.Any<CancellationToken>())
        .Returns(call => call.Arg<Func<CancellationToken, Task<Result<AppointmentResponse>>>>()(CancellationToken.None));
    private AppointmentService Service() => new(appointments, patients, users, uow, audit, clock);
    private Appointment Row(string status = AppointmentStatuses.Scheduled, int? dentist = null)
    {
        var row = new Appointment { AppointmentId = 7, PatientId = 9, AppointmentDate = clock.Today.AddDays(1), SlotTime = new(8, 0), Status = status, Notes = "Original note", DentistId = dentist };
        appointments.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(row);
        if (dentist.HasValue) users.FindByIdAsync(dentist.Value, Arg.Any<CancellationToken>()).Returns(new User { UserId = dentist.Value, IsActive = true, Role = new Role { RoleCode = RoleCodes.Dentist } });
        return row;
    }
    private RescheduleAppointmentRequest Request(string? reason = "Synthetic shift") => new(clock.Today.AddDays(2), new(9, 0), Reason: reason);
    private Task<Result<AppointmentResponse>> Move(RescheduleAppointmentRequest? request = null, string? role = RoleCodes.Receptionist)
        => Service().RescheduleAppointmentAsync(7, 10, role, request ?? Request(), null);
    [Fact] public async Task MovePreservesIdentityAndNotesAndRecordsMetadata()
    {
        var row = Row(); var result = await Move(Request("  Synthetic shift  "));
        Assert.True(result.IsSuccess); Assert.Equal(7, row.AppointmentId); Assert.Equal("Original note", row.Notes);
        Assert.Equal(1, row.RescheduleCount); Assert.Equal("Synthetic shift", row.RescheduleReason);
        Assert.Equal(1, result.Value!.RescheduleCount);
        var detail = (string)audit.ReceivedCalls().Single().GetArguments()[4]!;
        Assert.Contains("oldDate", detail); Assert.Contains("newTime", detail); Assert.DoesNotContain("Synthetic shift", detail);
    }
    [Theory] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)]
    public async Task UnsupportedRoleNeverStartsTransaction(string role)
    { Assert.Equal(Error.Forbidden, (await Move(role: role)).Error); Assert.Empty(uow.ReceivedCalls()); }
    [Fact] public async Task MissingAppointmentReturns404() => Assert.Equal(Error.AppointmentNotFound, (await Move()).Error);
    [Theory] [InlineData(AppointmentStatuses.CheckedIn)] [InlineData(AppointmentStatuses.Cancelled)] [InlineData(AppointmentStatuses.Completed)]
    public async Task OnlyScheduledCanMove(string state)
    { Row(state); Assert.Equal(Error.AppointmentCannotReschedule, (await Move()).Error); Assert.Empty(audit.ReceivedCalls()); }
    [Theory] [InlineData(null)] [InlineData(" ")] [InlineData("LONG")]
    public async Task MissingOrLongReasonCannotPersist(string? value)
    { Row(); Assert.True((await Move(Request(value == "LONG" ? new string('x', 501) : value))).IsFailure); await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>()); }
    [Fact] public async Task ReplayOfSameTargetIsNoOp()
    {
        var row = Row(); var request = new RescheduleAppointmentRequest(row.AppointmentDate, row.SlotTime, Reason: "Synthetic replay");
        Assert.True((await Move(request)).IsSuccess); Assert.Equal(0, row.RescheduleCount); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task FullSlotRejectsMove()
    {
        Row(); appointments.GetSlotBookingCountAsync(Request().AppointmentDate, Request().SlotTime, Arg.Any<CancellationToken>(), 7).Returns(100);
        Assert.Equal(Error.AppointmentSlotFull, (await Move()).Error); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task OtherPatientBookingOnTargetDayRejectsMove()
    {
        Row(); appointments.HasActiveAppointmentOnDateAsync(9, Request().AppointmentDate, Arg.Any<CancellationToken>(), 7).Returns(true);
        Assert.Equal(Error.AppointmentPatientHasActive, (await Move()).Error);
    }
    [Fact] public async Task DentistConflictRejectsWithoutMutating()
    {
        var row = Row(dentist: 30); appointments.HasDentistBookingAsync(30, Request().AppointmentDate, Request().SlotTime, 7, Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(Error.Validation.Code, (await Move()).Error.Code); Assert.Equal(0, row.RescheduleCount);
    }
    [Fact] public async Task InactiveDentistRejectsMove()
    {
        Row(dentist: 30); users.FindByIdAsync(30, Arg.Any<CancellationToken>()).Returns(new User { IsActive = false });
        Assert.Equal(Error.Validation.Code, (await Move()).Error.Code);
    }
    [Fact] public async Task PastDayCannotMove()
    { Row(); Assert.Equal(Error.AppointmentDateInPast, (await Move(new(clock.Today.AddDays(-1), new(9, 0), Reason: "Synthetic"))).Error); }
    [Fact] public async Task InvalidSlotCannotMove()
    { Row(); Assert.Equal(Error.AppointmentInvalidTime, (await Move(new(clock.Today.AddDays(2), new(12, 0), Reason: "Synthetic"))).Error); }
    [Fact] public async Task SerializableFailureReturnsConflict()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<AppointmentResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<AppointmentResponse>>(new ConcurrentOperationException()));
        Assert.Equal(Error.AppointmentConcurrent, (await Move()).Error);
    }
    [Theory] [InlineData(null, false)] [InlineData(" ", false)] [InlineData("Synthetic", true)] [InlineData("LONG", false)] [InlineData("BOUNDARY", true)]
    public void ValidatorRequiresBoundedReason(string? text, bool valid)
    {
        var value = text == "LONG" ? new string('x', 501) : text == "BOUNDARY" ? new string('x', 500) : text;
        Assert.Equal(valid, new RescheduleAppointmentRequestValidator().Validate(Request(value)).IsValid);
    }
}
