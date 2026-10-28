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
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)]
    public async Task StaffActorsBookActiveDentist(string role)
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
    [Theory]
    [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Patient)]
    public async Task RetiredEmergencyRejectedForEveryBookingRoleWithoutReadOrWrite(string role)
    {
        var result = await Create(Request() with { IsEmergency = true, EmergencyReason = "Synthetic attention" }, role);
        Assert.Equal(Error.Validation.Code, result.Error.Code);
        Assert.Contains("không tiếp nhận cấp cứu", result.Error.Message);
        Assert.Empty(uow.ReceivedCalls()); Assert.Empty(appointments.ReceivedCalls()); Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task RetiredOverrideFieldsRejectedEvenWithoutEmergencyFlag(int field)
    {
        var request = field switch { 0 => Request() with { EmergencyReason = "Legacy" },
            1 => Request() with { ConfirmedDentistConflictAppointmentId = 42 },
            _ => Request() with { ConfirmedDentistConflictRequestId = 17 } };
        Assert.Equal(Error.Validation.Code, (await Create(request)).Error.Code);
        Assert.Empty(uow.ReceivedCalls()); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task AttentionNoteDoesNotBypassDentistConflict()
    {
        appointments.GetDentistBookingConflictAsync(31, Request().AppointmentDate, Request().SlotTime, 0, Arg.Any<CancellationToken>(), durationMinutes: 30)
            .Returns(new Appointment { AppointmentId = 42 });
        Assert.Equal(Error.Validation.Code, (await Create(Request() with { Notes = "Synthetic attention" })).Error.Code);
        await appointments.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(6, 30)] [InlineData(17, 15)] [InlineData(23, 45)]
    public async Task AttentionNoteDoesNotPermitFreeTime(int hour, int minute)
    {
        Assert.Equal(Error.Validation.Code, (await Create(Request() with { SlotTime = new(hour, minute), Notes = "Synthetic attention" })).Error.Code);
        await appointments.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }
    [Fact] public async Task NormalSlotKeepsAttentionNoteWithoutEmergencyFields()
    {
        Appointment? saved = null;
        await appointments.AddAsync(Arg.Do<Appointment>(value => saved = value), Arg.Any<CancellationToken>());
        var result = await Create(Request() with { Notes = "  Synthetic attention  " });
        Assert.True(result.IsSuccess); Assert.Equal("Synthetic attention", saved!.Notes);
        Assert.False(saved.IsEmergency); Assert.Null(saved.EmergencyReason);
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
    [Fact] public async Task PatientCannotSelectDentistAndDoesNotStartTransaction()
    {
        var result = await Create(role: RoleCodes.Patient);
        Assert.Equal(Error.Validation.Code, result.Error.Code);
        Assert.Contains("không chọn nha sĩ", result.Error.Message);
        Assert.Empty(uow.ReceivedCalls()); Assert.Empty(patients.ReceivedCalls()); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task PatientCanBookOwnProfileWithoutSelectingDentist()
    {
        var result = await Create(Request(null), RoleCodes.Patient);
        Assert.True(result.IsSuccess); Assert.Null(result.Value!.DentistId);
        Assert.Empty(users.ReceivedCalls());
    }
    [Fact] public async Task PatientCannotBookUnownedProfileWithoutSelectingDentist()
    {
        patients.GetByIdAsync(9, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 9, UserId = 11, IsActive = true });
        Assert.Equal(Error.NotFound, (await Create(Request(null), RoleCodes.Patient)).Error); Assert.Empty(audit.ReceivedCalls());
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
    [Fact] public void ValidatorRejectsEveryRetiredFieldAndAcceptsNormalAttentionNote()
    {
        var validator = new CreateAppointmentRequestValidator();
        Assert.True(validator.Validate(Request() with { Notes = "Synthetic attention" }).IsValid);
        foreach (var request in new[] { Request() with { IsEmergency = true }, Request() with { EmergencyReason = "Legacy" },
            Request() with { ConfirmedDentistConflictAppointmentId = 42 }, Request() with { ConfirmedDentistConflictRequestId = 17 } })
            Assert.False(validator.Validate(request).IsValid);
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
