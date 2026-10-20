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

public sealed class DentistBookingTests
{
    private readonly IAppointmentRepository appointments = Substitute.For<IAppointmentRepository>();
    private readonly IPatientRepository patients = Substitute.For<IPatientRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero)));
    public DentistBookingTests()
    {
        patients.GetByIdAsync(9, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 9, UserId = 10, IsActive = true });
        users.FindByIdAsync(31, Arg.Any<CancellationToken>()).Returns(new User { UserId = 31, IsActive = true, Role = new Role { RoleCode = RoleCodes.Dentist } });
        appointments.AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>()).Returns(call => { call.Arg<Appointment>().AppointmentId = 7; return Task.CompletedTask; });
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<AppointmentResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<AppointmentResponse>>>>()(CancellationToken.None));
    }
    private AppointmentService Service() => new(appointments, patients, users, uow, audit, clock);
    private CreateAppointmentRequest Request(int? dentist = 31) => new(9, clock.Today.AddDays(1), new(9, 0), dentist, "Synthetic notes");
    private Task<Result<AppointmentResponse>> Create(CreateAppointmentRequest? request = null, string? role = RoleCodes.Receptionist)
        => Service().CreateAppointmentAsync(10, role, request ?? Request(), null);
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Patient)]
    public async Task SupportedActorsBookActiveDentist(string role)
    {
        var result = await Create(role: role); Assert.True(result.IsSuccess); Assert.Equal(31, result.Value!.DentistId);
        await appointments.Received(1).GetDentistBookingConflictAsync(31, Request().AppointmentDate, Request().SlotTime, 0, Arg.Any<CancellationToken>(), durationMinutes: 30);
        await uow.Received(1).ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<AppointmentResponse>>>>(), Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)] [InlineData(null)] [InlineData("UNKNOWN")]
    public async Task UnsupportedActorsNeverReadOrStartTransaction(string? role)
    { Assert.Equal(Error.Forbidden, (await Create(role: role)).Error); Assert.Empty(uow.ReceivedCalls()); Assert.Empty(patients.ReceivedCalls()); }
    [Fact] public async Task BusyDentistReturns400WithoutWriting()
    {
        appointments.GetDentistBookingConflictAsync(31, Request().AppointmentDate, Request().SlotTime, 0, Arg.Any<CancellationToken>(), durationMinutes: 30)
            .Returns(new Appointment { AppointmentId = 42, Patient = new Patient { FullName = "Đang có lịch" } });
        var result = await Create(); Assert.Equal(Error.Validation.Code, result.Error.Code); Assert.Contains("Bác sĩ đã có lịch", result.Error.Message);
        Assert.Empty(audit.ReceivedCalls()); await appointments.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }
    [Fact] public async Task EmergencyConflictReturnsNamedWarningAndRequiresMatchingConfirmation()
    {
        var request = Request() with { IsEmergency = true, EmergencyReason = "Đau răng dữ dội" };
        var conflict = new Appointment
        {
            AppointmentId = 42,
            AppointmentDate = request.AppointmentDate,
            SlotTime = request.SlotTime,
            Dentist = new User { FullName = "Nha sĩ A" },
            Patient = new Patient { FullName = "Bệnh nhân B" }
        };
        appointments.GetDentistBookingConflictAsync(31, request.AppointmentDate, request.SlotTime, 0, Arg.Any<CancellationToken>(), durationMinutes: 30)
            .Returns(conflict);

        var firstAttempt = await Create(request);
        Assert.Equal(Error.AppointmentDentistConflict.Code, firstAttempt.Error.Code);
        Assert.Contains("Nha sĩ A", firstAttempt.Error.Message);
        Assert.Contains("Bệnh nhân B", firstAttempt.Error.Message);
        Assert.Contains("09:00", firstAttempt.Error.Message);
        var details = Assert.IsType<AppointmentDentistConflictDetails>(firstAttempt.Error.Details);
        Assert.Equal(42, details.ConflictAppointmentId);
        await appointments.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
        Assert.Empty(audit.ReceivedCalls());

        var staleConfirmation = await Create(request with { ConfirmedDentistConflictAppointmentId = 43 });
        Assert.Equal(Error.AppointmentDentistConflict.Code, staleConfirmation.Error.Code);
        await appointments.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }
    [Fact] public async Task EmergencyConflictWithConfirmedAppointmentIsCreatedAndAuditedWithoutReasonText()
    {
        var request = Request() with { IsEmergency = true, EmergencyReason = "Đau răng dữ dội", ConfirmedDentistConflictAppointmentId = 42 };
        var conflict = new Appointment
        {
            AppointmentId = 42,
            AppointmentDate = request.AppointmentDate,
            SlotTime = request.SlotTime,
            Dentist = new User { FullName = "Nha sĩ A" },
            Patient = new Patient { FullName = "Bệnh nhân B" }
        };
        appointments.GetDentistBookingConflictAsync(31, request.AppointmentDate, request.SlotTime, 0, Arg.Any<CancellationToken>(), durationMinutes: 30)
            .Returns(conflict);
        Appointment? saved = null;
        string? auditDetail = null;
        await appointments.AddAsync(Arg.Do<Appointment>(value => saved = value), Arg.Any<CancellationToken>());
        await audit.LogAsync(AuditActions.AppointmentCreated, "Appointment", Arg.Any<int>(), Arg.Any<int>(),
            Arg.Do<string?>(value => auditDetail = value), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var result = await Create(request);
        Assert.True(result.IsSuccess);
        Assert.True(saved!.IsEmergency);
        Assert.Equal("Đau răng dữ dội", saved.EmergencyReason);
        Assert.Contains("confirmedDentistConflictOverride\":true", auditDetail);
        Assert.DoesNotContain("Đau răng dữ dội", auditDetail);
    }
    [Fact] public async Task PatientCannotCreateEmergencyBooking()
    {
        var result = await Create(Request() with { IsEmergency = true, EmergencyReason = "Đau răng dữ dội" }, RoleCodes.Patient);
        Assert.Equal(Error.Forbidden, result.Error);
        Assert.Empty(uow.ReceivedCalls());
        Assert.Empty(patients.ReceivedCalls());
    }
    [Fact] public async Task EmergencyBookingCanStartImmediatelyInTheCurrentDay()
    {
        var request = Request() with
        {
            AppointmentDate = clock.Today,
            SlotTime = new TimeOnly(6, 30),
            IsEmergency = true,
            EmergencyReason = "Đau răng dữ dội"
        };

        var result = await Create(request);

        Assert.True(result.IsSuccess);
        await appointments.Received(1).AddAsync(Arg.Is<Appointment>(appointment => appointment.IsEmergency), Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(false, RoleCodes.Dentist)] [InlineData(true, RoleCodes.Receptionist)]
    public async Task InactiveOrWrongRoleCannotBeAssigned(bool active, string role)
    {
        users.FindByIdAsync(31, Arg.Any<CancellationToken>()).Returns(new User { IsActive = active, Role = new Role { RoleCode = role } });
        Assert.Equal(Error.Validation.Code, (await Create()).Error.Code);
    }
    [Fact] public async Task OptionalUnassignedBookingStillWorks()
    {
        Assert.True((await Create(Request(null))).IsSuccess); Assert.Empty(users.ReceivedCalls());
        await appointments.DidNotReceive().HasDentistBookingAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), durationMinutes: 30);
    }
    [Fact] public async Task PatientCannotBookUnownedProfileEvenWithValidDentist()
    {
        patients.GetByIdAsync(9, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 9, UserId = 11, IsActive = true });
        Assert.Equal(Error.NotFound, (await Create(role: RoleCodes.Patient)).Error); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task SerializableConflictHasBusinessResponse()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<AppointmentResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<AppointmentResponse>>(new ConcurrentOperationException()));
        Assert.Equal(Error.AppointmentConcurrent, (await Create()).Error);
    }
    [Theory] [InlineData(null, true)] [InlineData(31, true)] [InlineData(0, false)] [InlineData(-1, false)]
    public void DentistValidatorPreservesOptionalSelection(int? id, bool valid)
        => Assert.Equal(valid, new CreateAppointmentRequestValidator().Validate(Request(id)).IsValid);
    [Fact] public void EmergencyValidatorAllowsAnyMinuteButRequiresReason()
    {
        var validator = new CreateAppointmentRequestValidator();
        Assert.True(validator.Validate(Request() with { SlotTime = new(17, 15), IsEmergency = true, EmergencyReason = "Đau răng dữ dội" }).IsValid);
        Assert.False(validator.Validate(Request() with { SlotTime = new(17, 15), IsEmergency = true, EmergencyReason = "đau" }).IsValid);
        Assert.False(validator.Validate(Request() with { SlotTime = new(17, 15, 1), IsEmergency = true, EmergencyReason = "Đau răng dữ dội" }).IsValid);
        Assert.False(validator.Validate(Request() with { SlotTime = new TimeOnly(17, 15).Add(TimeSpan.FromTicks(1)), IsEmergency = true, EmergencyReason = "Đau răng dữ dội" }).IsValid);
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)]
    public async Task StaffDentistPickerOnlyProjectsIdAndName(string role)
    {
        users.GetActiveDentistsAsync(Arg.Any<CancellationToken>()).Returns(new[] { new User { UserId = 31, FullName = "Synthetic dentist", Phone = "0999999931", PasswordHash = "Synthetic non-secret" } });
        var result = await Service().GetBookingDentistsAsync(role); Assert.Equal(new BookingDentistResponse(31, "Synthetic dentist"), Assert.Single(result.Value!));
        Assert.Empty(audit.ReceivedCalls()); Assert.Empty(uow.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)]
    public async Task PickerDeniedRolesNeverReadStaff(string role)
    { Assert.Equal(Error.Forbidden, (await Service().GetBookingDentistsAsync(role)).Error); Assert.Empty(users.ReceivedCalls()); }
}
