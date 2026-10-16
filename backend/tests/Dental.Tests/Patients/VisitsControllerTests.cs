using Dental.Api.Controllers;
using Dental.Application.Common;
using Dental.Application.Features.Visits.DTOs;
using Dental.Application.Features.Visits.Services;
using Dental.Application.Features.Visits.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;
using VisitService = Dental.Application.Features.Visits.Services.VisitService;

namespace Dental.Tests.Patients;

public sealed class VisitsControllerTests
{
    private static IUnitOfWork CreateUnitOfWork()
    {
        var uow = Substitute.For<IUnitOfWork>();
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<VisitResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<VisitResponse>>>>()(CancellationToken.None));
        return uow;
    }

    private static VisitsController CreateController(
        VisitService service,
        ICurrentUser currentUser,
        PostTreatmentNoteService? noteService = null)
    {
        noteService ??= new PostTreatmentNoteService(
            Substitute.For<IVisitRepository>(),
            Substitute.For<IPatientRepository>(),
            Substitute.For<IInvoiceRepository>(),
            Substitute.For<IPostTreatmentNoteRepository>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IAuditLogger>());

        return new VisitsController(
            service,
            noteService,
            currentUser,
            new VisitQueryRequestValidator(),
            new UpdateVisitDiagnosisRequestValidator(),
            new CreatePostTreatmentNoteRequestValidator())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

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
            CreateUnitOfWork(),
            Substitute.For<IAuditLogger>());
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(8);
        var controller = CreateController(service, currentUser);

        var result = await controller.CreateVisit(5, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        await visitRepository.DidNotReceive().AddAsync(Arg.Any<Visit>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateDiagnosis_ValidRequest_ReturnsOk()
    {
        var visitRepository = Substitute.For<IVisitRepository>();
        var visit = new Visit
        {
            VisitId = 15,
            PatientId = 5,
            Status = VisitStatuses.InProgress,
            DentistId = 10,
        };
        visitRepository.GetForUpdateAsync(15, Arg.Any<CancellationToken>()).Returns(visit);

        var unitOfWork = CreateUnitOfWork();
        var auditLogger = Substitute.For<IAuditLogger>();
        var service = new VisitService(
            Substitute.For<IPatientRepository>(),
            visitRepository,
            unitOfWork,
            auditLogger);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(10);
        currentUser.RoleCode.Returns(RoleCodes.Dentist);

        var controller = CreateController(service, currentUser);

        var result = await controller.UpdateDiagnosis(
            15,
            new UpdateVisitDiagnosisRequest("Sâu răng 16", "Cần hàn răng"),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<VisitResponse>(okResult.Value);
        Assert.Equal("Sâu răng 16", response.Diagnosis);
        Assert.Equal("Cần hàn răng", response.ClinicalNotes);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateDiagnosis_InvalidInput_ReturnsBadRequest()
    {
        var service = new VisitService(
            Substitute.For<IPatientRepository>(),
            Substitute.For<IVisitRepository>(),
            CreateUnitOfWork(),
            Substitute.For<IAuditLogger>());
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(10);

        var controller = CreateController(service, currentUser);

        var result = await controller.UpdateDiagnosis(
            15,
            new UpdateVisitDiagnosisRequest(""),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task UpdateDiagnosis_DentistMismatch_ReturnsForbidden()
    {
        var visitRepository = Substitute.For<IVisitRepository>();
        var visit = new Visit
        {
            VisitId = 15,
            PatientId = 5,
            Status = VisitStatuses.InProgress,
            DentistId = 10,
        };
        visitRepository.GetForUpdateAsync(15, Arg.Any<CancellationToken>()).Returns(visit);

        var service = new VisitService(
            Substitute.For<IPatientRepository>(),
            visitRepository,
            CreateUnitOfWork(),
            Substitute.For<IAuditLogger>());

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(99); // different dentist
        currentUser.RoleCode.Returns(RoleCodes.Dentist);

        var controller = CreateController(service, currentUser);

        var result = await controller.UpdateDiagnosis(
            15,
            new UpdateVisitDiagnosisRequest("Sâu răng"),
            CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }
}
