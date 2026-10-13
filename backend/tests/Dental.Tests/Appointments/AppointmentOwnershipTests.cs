using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Appointments.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;
namespace Dental.Tests.Appointments;
public sealed class AppointmentOwnershipTests
{
    private readonly IAppointmentRepository appointments = Substitute.For<IAppointmentRepository>();
    private readonly IPatientRepository patients = Substitute.For<IPatientRepository>();
    private AppointmentService Service() => new(appointments, patients, Substitute.For<IUserRepository>(), Substitute.For<IUnitOfWork>(), Substitute.For<IAuditLogger>(), new VietnamClock(TimeProvider.System));
    [Fact] public async Task PatientListAlwaysPassesOwnerFilter()
    {
        appointments.GetAppointmentsAsync(null, null, null, 1, 20, Arg.Any<CancellationToken>(), 7).Returns((Array.Empty<Appointment>(), 0));
        var result = await Service().GetAccessibleAppointmentsAsync(new(), 7, RoleCodes.Patient);
        Assert.True(result.IsSuccess); await appointments.Received(1).GetAppointmentsAsync(null, null, null, 1, 20, Arg.Any<CancellationToken>(), 7);
    }
    [Fact] public async Task OtherPatientFilterReturns404BeforeListing()
    {
        patients.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 8, UserId = 9 });
        var result = await Service().GetAccessibleAppointmentsAsync(new(PatientId: 8), 7, RoleCodes.Patient);
        Assert.Equal(Error.NotFound, result.Error); Assert.Empty(appointments.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)]
    public async Task StaffListsKeepExistingScope(string role)
    {
        appointments.GetAppointmentsAsync(null, null, null, 1, 20, Arg.Any<CancellationToken>(), null).Returns((Array.Empty<Appointment>(), 0));
        var result = await Service().GetAccessibleAppointmentsAsync(new(), 7, role); Assert.True(result.IsSuccess);
        await appointments.Received(1).GetAppointmentsAsync(null, null, null, 1, 20, Arg.Any<CancellationToken>(), null);
    }
    [Theory] [InlineData(7, true)] [InlineData(8, false)]
    public async Task DetailOnlyForOwnedProfile(int owner, bool allowed)
    {
        var patient = new Patient { PatientId = 2, UserId = owner, FullName = "Synthetic patient" };
        patients.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(patient);
        appointments.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Appointment { AppointmentId = 1, PatientId = 2, Patient = patient });
        var result = await Service().GetAccessibleAppointmentByIdAsync(1, 7, RoleCodes.Patient);
        Assert.Equal(allowed, result.IsSuccess); if (!allowed) Assert.Equal(Error.AppointmentNotFound, result.Error);
    }
    [Fact] public async Task MissingDetailReturns404()
        => Assert.Equal(Error.AppointmentNotFound, (await Service().GetAccessibleAppointmentByIdAsync(999, 7, RoleCodes.Patient)).Error);
    [Theory] [InlineData(null)] [InlineData("UNKNOWN")]
    public async Task UnknownRoleCannotRead(string? role)
    {
        Assert.Equal(Error.Forbidden, (await Service().GetAccessibleAppointmentsAsync(new(), 7, role)).Error);
        Assert.Equal(Error.Forbidden, (await Service().GetAccessibleAppointmentByIdAsync(1, 7, role)).Error); Assert.Empty(appointments.ReceivedCalls());
    }
    [Fact] public void ListAndDetailResponsesAreNotCached()
    {
        foreach (var name in new[] { "GetAppointments", "GetAppointmentById" })
            Assert.True(Assert.Single(typeof(Dental.Api.Controllers.AppointmentsController).GetMethod(name)!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.ResponseCacheAttribute), true).Cast<Microsoft.AspNetCore.Mvc.ResponseCacheAttribute>()).NoStore);
    }
}
