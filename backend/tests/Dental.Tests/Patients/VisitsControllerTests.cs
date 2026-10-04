using Dental.Api.Controllers;
using Dental.Application.Features.Visits.DTOs;
using Dental.Application.Features.Visits.Services;
using Dental.Application.Features.Visits.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Patients;

public sealed class VisitsControllerTests
{
    [Fact]
    public async Task CreateVisit_InactivePatientReturnsConflict()
    {
        var patientRepository = Substitute.For<IPatientRepository>();
        patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5, IsActive = false });
        var visitRepository = Substitute.For<IVisitRepository>();
        var service = new VisitService(
            patientRepository,
            visitRepository,
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IAuditLogger>());
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(8);
        var controller = new VisitsController(service, currentUser, new VisitQueryRequestValidator())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.CreateVisit(5, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        await visitRepository.DidNotReceive().AddAsync(Arg.Any<Visit>(), Arg.Any<CancellationToken>());
    }
}
