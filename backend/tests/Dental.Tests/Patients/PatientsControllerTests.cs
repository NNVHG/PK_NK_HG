using Dental.Api.Controllers;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Services;
using Dental.Application.Features.Patients.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Patients;

public sealed class PatientsControllerTests
{
    [Fact]
    public async Task GetPatient_WhenPatientRequestsAnotherUsersRecord_ReturnsNotFound()
    {
        var patientRepository = Substitute.For<IPatientRepository>();
        patientRepository.GetByIdAsync(31, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 31, PatientNumber = 31, UserId = 22 });
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(11);
        currentUser.RoleCode.Returns(RoleCodes.Patient);
        var service = new PatientsService(
            patientRepository,
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IAuditLogger>());
        var controller = new PatientsController(
            service,
            currentUser,
            new CreatePatientRequestValidator(),
            new PatientDuplicateCheckRequestValidator(),
            new PatientQueryRequestValidator(),
            new UpdatePatientRequestValidator())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.GetPatient(31, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPatients_WhenPatientRequestsSearch_ReturnsForbidden()
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(11);
        currentUser.RoleCode.Returns(RoleCodes.Patient);
        var service = new PatientsService(
            Substitute.For<IPatientRepository>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IAuditLogger>());
        var controller = new PatientsController(
            service,
            currentUser,
            new CreatePatientRequestValidator(),
            new PatientDuplicateCheckRequestValidator(),
            new PatientQueryRequestValidator(),
            new UpdatePatientRequestValidator());

        var result = await controller.GetPatients(new PatientQueryRequest(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }
}
