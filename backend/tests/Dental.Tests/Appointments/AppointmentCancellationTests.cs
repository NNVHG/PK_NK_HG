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
public sealed class AppointmentCancellationTests
{
    [Theory]
    [InlineData(0, 20, null, null)] [InlineData(1000001, 20, null, null)]
    [InlineData(1, 101, null, null)] [InlineData(1, 20, "Unknown", null)] [InlineData(1, 20, null, 0)]
    public void InvalidDeskQueriesAreRejected(int page, int size, string? status, int? patient)
        => Assert.False(new Dental.Application.Features.Appointments.Validators.AppointmentQueryRequestValidator()
            .Validate(new AppointmentQueryRequest(Status: status, PatientId: patient, Page: page, PageSize: size)).IsValid);
    [Fact] public void DeskEndpointRequiresManagementPolicyAndNoCache()
    {
        var method = typeof(Dental.Api.Controllers.AppointmentsController).GetMethod("GetDeskAppointments")!;
        Assert.Equal(Dental.Api.Policies.Policies.AppointmentManage, Assert.Single(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()).Policy);
        Assert.True(Assert.Single(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.ResponseCacheAttribute), true).Cast<Microsoft.AspNetCore.Mvc.ResponseCacheAttribute>()).NoStore);
    }
    private readonly IAppointmentRepository appointments = Substitute.For<IAppointmentRepository>();
    private readonly IPatientRepository patients = Substitute.For<IPatientRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 12, 5, 0, 0, TimeSpan.Zero)));
    private AppointmentService Service() => new(appointments, patients, Substitute.For<IUserRepository>(), uow, audit, clock);
    private Appointment Row(int minutes = 120, string status = AppointmentStatuses.Scheduled, string? notes = null)
    {
        var time = clock.Now.AddMinutes(minutes);
        var row = new Appointment { AppointmentId = 9, PatientId = 2, AppointmentDate = DateOnly.FromDateTime(time), SlotTime = TimeOnly.FromDateTime(time), Status = status, Notes = notes };
        appointments.GetByIdAsync(9, Arg.Any<CancellationToken>()).Returns(row);
        patients.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 2, UserId = 7 });
        return row;
    }
    [Theory] [InlineData(119, false)] [InlineData(120, true)] [InlineData(121, true)]
    public async Task PatientBoundaryUsesVietnamTime(int minutes, bool allowed)
    {
        Row(minutes); var result = await Service().CancelAppointmentAsync(9, 7, RoleCodes.Patient, new(), null);
        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed) Assert.Empty(uow.ReceivedCalls());
    }
    [Fact] public async Task OtherAccountIsNotFoundWithoutSaving()
    {
        Row(); var result = await Service().CancelAppointmentAsync(9, 8, RoleCodes.Patient, new(), null);
        Assert.Equal(Error.AppointmentNotFound, result.Error); Assert.Empty(uow.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)]
    public async Task StaffMayCancelNearAppointment(string role)
    {
        var row = Row(0); var result = await Service().CancelAppointmentAsync(9, 8, role, new("Synthetic reason"), null);
        Assert.True(result.IsSuccess); Assert.Equal(AppointmentStatuses.Cancelled, row.Status);
    }
    [Theory] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)] [InlineData(null)]
    public async Task UnsupportedRoleCannotReadOrWrite(string? role)
    {
        var result = await Service().CancelAppointmentAsync(9, 8, role, new(), null);
        Assert.Equal(Error.Forbidden, result.Error); Assert.Empty(appointments.ReceivedCalls());
    }
    [Theory] [InlineData(AppointmentStatuses.CheckedIn)] [InlineData(AppointmentStatuses.Completed)] [InlineData(AppointmentStatuses.Cancelled)]
    public async Task OnlyScheduledCanBeCancelled(string state)
    {
        Row(status: state); var result = await Service().CancelAppointmentAsync(9, 8, RoleCodes.Admin, new(), null);
        Assert.Equal(Error.AppointmentCannotCancel, result.Error); Assert.Empty(uow.ReceivedCalls());
    }
    [Fact] public async Task OversizedCombinedNotesDoNotMutateOrPersist()
    {
        var row = Row(notes: new string('x', 500));
        var result = await Service().CancelAppointmentAsync(9, 8, RoleCodes.Admin, new("Synthetic"), null);
        Assert.Equal(Error.Validation.Code, result.Error.Code); Assert.Equal(AppointmentStatuses.Scheduled, row.Status); Assert.Equal(500, row.Notes!.Length); Assert.Empty(uow.ReceivedCalls());
    }
    [Fact] public async Task BlankReasonPreservesOriginalNotes()
    {
        var row = Row(notes: new string('x', 500)); var result = await Service().CancelAppointmentAsync(9, 8, RoleCodes.Admin, new("  "), null);
        Assert.True(result.IsSuccess); Assert.Equal(500, row.Notes!.Length);
    }
}
