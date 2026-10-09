using Dental.Application.Common;
using Dental.Api.Controllers;
using ApiPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Services;
using Dental.Application.Features.Patients.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Patients;

public sealed class PatientsControllerTests
{
    [Fact]
    public void GetMyProfiles_RequiresPatientViewPolicy()
    {
        var method = typeof(PatientsController).GetMethod(nameof(PatientsController.GetMyProfiles));

        Assert.NotNull(method);
        var authorize = Assert.Single(method!.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(ApiPolicies.PatientView, authorize.Policy);
    }

    [Fact]
    public async Task GetMyProfiles_WhenCalledByPatient_ReturnsProfilesForCurrentAccount()
    {
        var repository = Substitute.For<IPatientRepository>();
        repository.GetActiveByUserIdAsync(11, Arg.Any<CancellationToken>())
            .Returns(new[] { new Patient { PatientId = 31, PatientNumber = 31, UserId = 11, FullName = "An" } });
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(11);
        currentUser.RoleCode.Returns(RoleCodes.Patient);
        var service = new PatientsService(repository, Substitute.For<IUnitOfWork>(), Substitute.For<IAuditLogger>());
        var controller = new PatientsController(
            service,
            currentUser,
            new CreatePatientRequestValidator(new VietnamClock(TimeProvider.System)),
            new PatientDuplicateCheckRequestValidator(new VietnamClock(TimeProvider.System)),
            new PatientQueryRequestValidator(),
            new UpdatePatientRequestValidator(new VietnamClock(TimeProvider.System)));

        var result = await controller.GetMyProfiles(CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        var profiles = Assert.IsAssignableFrom<IReadOnlyList<PatientListItemResponse>>(response.Value);
        Assert.Single(profiles);
        Assert.Equal(31, profiles[0].PatientId);
        await repository.Received(1).GetActiveByUserIdAsync(11, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMyProfiles_WhenCalledByStaff_ReturnsForbidden()
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.RoleCode.Returns(RoleCodes.Receptionist);
        var controller = new PatientsController(
            new PatientsService(Substitute.For<IPatientRepository>(), Substitute.For<IUnitOfWork>(), Substitute.For<IAuditLogger>()),
            currentUser,
            new CreatePatientRequestValidator(new VietnamClock(TimeProvider.System)),
            new PatientDuplicateCheckRequestValidator(new VietnamClock(TimeProvider.System)),
            new PatientQueryRequestValidator(),
            new UpdatePatientRequestValidator(new VietnamClock(TimeProvider.System)));

        var result = await controller.GetMyProfiles(CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

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
            new CreatePatientRequestValidator(new VietnamClock(TimeProvider.System)),
            new PatientDuplicateCheckRequestValidator(new VietnamClock(TimeProvider.System)),
            new PatientQueryRequestValidator(),
            new UpdatePatientRequestValidator(new VietnamClock(TimeProvider.System)))
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
            new CreatePatientRequestValidator(new VietnamClock(TimeProvider.System)),
            new PatientDuplicateCheckRequestValidator(new VietnamClock(TimeProvider.System)),
            new PatientQueryRequestValidator(),
            new UpdatePatientRequestValidator(new VietnamClock(TimeProvider.System)));

        var result = await controller.GetPatients(new PatientQueryRequest(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }
}
