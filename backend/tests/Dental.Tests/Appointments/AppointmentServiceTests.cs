using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Appointments.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Tests.Common;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Appointments;

public sealed class AppointmentServiceTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly FixedTimeProvider _timeProvider;
    private readonly VietnamClock _vietnamClock;

    public AppointmentServiceTests()
    {
        // 2026-10-10 09:00:00 UTC (16:00:00 giờ Việt Nam)
        _timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero));
        _vietnamClock = new VietnamClock(_timeProvider);
    }

    private AppointmentService CreateService()
        => new(_appointmentRepository, _patientRepository, _userRepository, _unitOfWork, _auditLogger, _vietnamClock);

    private static Patient MakePatient(int patientId = 1, bool isActive = true)
        => new()
        {
            PatientId = patientId,
            PatientNumber = 1001,
            FullName = "Nguyễn Văn A",
            Phone = "0901234567",
            IsActive = isActive
        };

    [Fact]
    public async Task CreateAppointmentAsync_ValidFutureSlot_CreatesScheduledAppointmentAndAudits()
    {
        var tomorrow = _vietnamClock.Today.AddDays(1);
        var slot = new TimeOnly(8, 30);
        var patient = MakePatient();

        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(patient);
        _appointmentRepository.HasActiveAppointmentOnDateAsync(1, tomorrow, Arg.Any<CancellationToken>()).Returns(false);
        _appointmentRepository.GetSlotBookingCountAsync(tomorrow, slot, Arg.Any<CancellationToken>()).Returns(5);

        _appointmentRepository.AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.Arg<Appointment>().AppointmentId = 10;
            return Task.CompletedTask;
        });

        var result = await CreateService().CreateAppointmentAsync(
            actorUserId: 2,
            new CreateAppointmentRequest(1, tomorrow, slot, null, "Khám tổng quát"),
            ipAddress: "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatuses.Scheduled, result.Value!.Status);
        Assert.Equal(tomorrow, result.Value.AppointmentDate);
        Assert.Equal(slot, result.Value.SlotTime);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.AppointmentCreated,
            entityType: "Appointment",
            entityId: 10,
            userId: 2,
            detail: Arg.Is<string?>(d => d != null && d.Contains("AppointmentDate")),
            ipAddress: "127.0.0.1",
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAppointmentAsync_PastDate_ReturnsAppointmentDateInPast()
    {
        var yesterday = _vietnamClock.Today.AddDays(-1);
        var patient = MakePatient();
        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(patient);

        var result = await CreateService().CreateAppointmentAsync(
            actorUserId: 2,
            new CreateAppointmentRequest(1, yesterday, new TimeOnly(8, 30)),
            ipAddress: "127.0.0.1");

        Assert.True(result.IsFailure);
        Assert.Equal(Error.AppointmentDateInPast.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateAppointmentAsync_SlotCapacity100Reached_ReturnsAppointmentSlotFull()
    {
        var tomorrow = _vietnamClock.Today.AddDays(1);
        var slot = new TimeOnly(9, 0);
        var patient = MakePatient();

        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(patient);
        _appointmentRepository.HasActiveAppointmentOnDateAsync(1, tomorrow, Arg.Any<CancellationToken>()).Returns(false);
        _appointmentRepository.GetSlotBookingCountAsync(tomorrow, slot, Arg.Any<CancellationToken>()).Returns(100);

        var result = await CreateService().CreateAppointmentAsync(
            actorUserId: 2,
            new CreateAppointmentRequest(1, tomorrow, slot),
            ipAddress: "127.0.0.1");

        Assert.True(result.IsFailure);
        Assert.Equal(Error.AppointmentSlotFull.Code, result.Error.Code);
    }
    [Fact]
    public async Task CreateAppointmentAsync_PatientAlreadyHasAppointmentOnSameDate_ReturnsPatientHasActive()
    {
        var tomorrow = _vietnamClock.Today.AddDays(1);
        var patient = MakePatient();

        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(patient);
        _appointmentRepository.HasActiveAppointmentOnDateAsync(1, tomorrow, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateService().CreateAppointmentAsync(
            actorUserId: 2,
            new CreateAppointmentRequest(1, tomorrow, new TimeOnly(10, 0)),
            ipAddress: "127.0.0.1");

        Assert.True(result.IsFailure);
        Assert.Equal(Error.AppointmentPatientHasActive.Code, result.Error.Code);
    }

    [Fact]
    public async Task CancelAppointmentAsync_ScheduledAppointment_CancelsSuccessfully()
    {
        var appointment = new Appointment
        {
            AppointmentId = 5,
            PatientId = 1,
            AppointmentDate = _vietnamClock.Today.AddDays(2),
            SlotTime = new TimeOnly(9, 30),
            Status = AppointmentStatuses.Scheduled
        };
        _appointmentRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(appointment);

        var result = await CreateService().CancelAppointmentAsync(
            appointmentId: 5,
            actorUserId: 2,
            new CancelAppointmentRequest("Bận việc đột xuất"),
            ipAddress: "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatuses.Cancelled, appointment.Status);
        Assert.Contains("Bận việc đột xuất", appointment.Notes);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RescheduleAppointmentAsync_ScheduledAppointment_UpdatesSlotAndAudits()
    {
        var originalDate = _vietnamClock.Today.AddDays(2);
        var newDate = _vietnamClock.Today.AddDays(3);
        var appointment = new Appointment
        {
            AppointmentId = 5,
            PatientId = 1,
            AppointmentDate = originalDate,
            SlotTime = new TimeOnly(8, 30),
            Status = AppointmentStatuses.Scheduled
        };
        _appointmentRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(appointment);
        _appointmentRepository.GetSlotBookingCountAsync(newDate, new TimeOnly(14, 0), Arg.Any<CancellationToken>()).Returns(10);

        var result = await CreateService().RescheduleAppointmentAsync(
            appointmentId: 5,
            actorUserId: 2,
            new RescheduleAppointmentRequest(newDate, new TimeOnly(14, 0)),
            ipAddress: "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(newDate, appointment.AppointmentDate);
        Assert.Equal(new TimeOnly(14, 0), appointment.SlotTime);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

}
