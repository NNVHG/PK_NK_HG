using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Visits.DTOs;
using Dental.Application.Features.Visits.Services;
using Dental.Application.Features.Visits.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;
using VisitService = Dental.Application.Features.Visits.Services.VisitService;

namespace Dental.Tests.Patients;

public sealed class VisitServiceTests
{
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly IVisitRepository _visitRepository = Substitute.For<IVisitRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private VisitService CreateService() => new(_patientRepository, _visitRepository, _unitOfWork, _auditLogger);

    [Fact]
    public async Task CreateVisitAsync_CreatesNewRecordAndPreservesPreviousVisit()
    {
        var previousVisit = new Visit
        {
            VisitId = 12,
            PatientId = 5,
            Status = VisitStatuses.Completed,
            StartedAt = new DateTime(2025, 4, 3, 9, 0, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2025, 4, 3, 9, 45, 0, DateTimeKind.Utc),
            CreatedByUserId = 2,
            CreatedAt = new DateTime(2025, 4, 3, 8, 55, 0, DateTimeKind.Utc),
        };
        var visits = new List<Visit> { previousVisit };
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5, PatientNumber = 50, IsActive = true });
        _visitRepository.HasOpenVisitAsync(5, Arg.Any<CancellationToken>()).Returns(false);
        _visitRepository.AddAsync(Arg.Any<Visit>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var newVisit = call.Arg<Visit>();
            newVisit.VisitId = 13;
            visits.Add(newVisit);
            return Task.CompletedTask;
        });

        var result = await CreateService().CreateVisitAsync(5, 8, "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, visits.Count);
        Assert.Same(previousVisit, visits[0]);
        Assert.Equal(VisitStatuses.Completed, visits[0].Status);
        Assert.Equal(new DateTime(2025, 4, 3, 9, 45, 0, DateTimeKind.Utc), visits[0].EndedAt);
        Assert.NotEqual(visits[0].VisitId, visits[1].VisitId);
        Assert.Equal(VisitStatuses.Created, visits[1].Status);
        Assert.Null(visits[1].StartedAt);
        Assert.Null(visits[1].EndedAt);
        Assert.Equal(8, visits[1].CreatedByUserId);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.VisitCreated,
            entityType: "Visit",
            entityId: 13,
            userId: 8,
            detail: Arg.Is<string?>(detail => detail != null && detail.Contains("create_visit")),
            ipAddress: "127.0.0.1",
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateVisitAsync_PatientDoesNotExist_ReturnsNotFound()
    {
        _patientRepository.GetByIdAsync(404, Arg.Any<CancellationToken>()).Returns((Patient?)null);

        var result = await CreateService().CreateVisitAsync(404, 8, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound, result.Error);
        await _visitRepository.DidNotReceive().HasOpenVisitAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _visitRepository.DidNotReceive().AddAsync(Arg.Any<Visit>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateVisitAsync_InactivePatient_ReturnsConflict()
    {
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5, IsActive = false });

        var result = await CreateService().CreateVisitAsync(5, 8, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.PatientInactive, result.Error);
        await _visitRepository.DidNotReceive().HasOpenVisitAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _visitRepository.DidNotReceive().AddAsync(Arg.Any<Visit>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateVisitAsync_OpenVisitExists_RejectsNewVisit()
    {
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5 });
        _visitRepository.HasOpenVisitAsync(5, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateService().CreateVisitAsync(5, 8, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.VisitAlreadyOpen, result.Error);
        await _visitRepository.DidNotReceive().AddAsync(Arg.Any<Visit>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPatientVisitsAsync_PatientRequestsAnotherPatientsHistory_ReturnsNotFound()
    {
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5, UserId = 99 });

        var result = await CreateService().GetPatientVisitsAsync(
            5, 10, RoleCodes.Patient, new VisitQueryRequest());

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound, result.Error);
        await _visitRepository.DidNotReceive().GetPatientVisitsAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VisitQueryRequestValidator_RejectsInvalidPagination()
    {
        var result = await new VisitQueryRequestValidator().ValidateAsync(
            new VisitQueryRequest { Page = 0, PageSize = 101 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(VisitQueryRequest.Page));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(VisitQueryRequest.PageSize));
    }

    [Fact]
    public async Task UpdateDiagnosisAsync_Success_WhenAssignedDentistUpdatesInProgressVisit()
    {
        var visit = new Visit
        {
            VisitId = 20,
            PatientId = 7,
            DentistId = 12,
            Status = VisitStatuses.InProgress,
        };
        _visitRepository.GetForUpdateAsync(20, Arg.Any<CancellationToken>()).Returns(visit);

        var request = new UpdateVisitDiagnosisRequest("Viêm tủy răng 26", "Đã đặt thuốc diệt tủy");
        var result = await CreateService().UpdateDiagnosisAsync(
            20, request, 12, RoleCodes.Dentist, "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal("Viêm tủy răng 26", visit.Diagnosis);
        Assert.Equal("Đã đặt thuốc diệt tủy", visit.ClinicalNotes);
        Assert.NotNull(visit.UpdatedAt);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.VisitDiagnosisUpdated,
            entityType: "Visit",
            entityId: 20,
            userId: 12,
            detail: Arg.Is<string?>(d => d != null && d.Contains("\"visitId\":20") && d.Contains("changedFields")
                && !d.Contains(JsonSerializer.Serialize(request.Diagnosis))
                && !d.Contains(JsonSerializer.Serialize(request.ClinicalNotes))),
            ipAddress: "127.0.0.1",
            ct: Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Assistant)]
    [InlineData(RoleCodes.Patient)]
    [InlineData("UNKNOWN")]
    [InlineData(null)]
    public async Task UpdateDiagnosisAsync_UnsupportedRole_CannotLoadOrChangeVisit(string? role)
    {
        var result = await CreateService().UpdateDiagnosisAsync(
            20, new UpdateVisitDiagnosisRequest("Sâu răng"), 12, role, null);

        Assert.Equal(Error.Forbidden, result.Error);
        await _visitRepository.DidNotReceive().GetForUpdateAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateDiagnosisAsync_Success_WhenAdminUpdatesInProgressVisit()
    {
        var visit = new Visit
        {
            VisitId = 20,
            PatientId = 7,
            DentistId = 12,
            Status = VisitStatuses.InProgress,
        };
        _visitRepository.GetForUpdateAsync(20, Arg.Any<CancellationToken>()).Returns(visit);

        var request = new UpdateVisitDiagnosisRequest("Viêm nướu", null);
        var result = await CreateService().UpdateDiagnosisAsync(
            20, request, 99, RoleCodes.Admin, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("Viêm nướu", visit.Diagnosis);
        Assert.Null(visit.ClinicalNotes);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateDiagnosisAsync_MismatchedDentist_ReturnsDentistMismatchError()
    {
        var visit = new Visit
        {
            VisitId = 20,
            PatientId = 7,
            DentistId = 12,
            Status = VisitStatuses.InProgress,
        };
        _visitRepository.GetForUpdateAsync(20, Arg.Any<CancellationToken>()).Returns(visit);

        var request = new UpdateVisitDiagnosisRequest("Sâu răng", null);
        var result = await CreateService().UpdateDiagnosisAsync(
            20, request, 99, RoleCodes.Dentist, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.DiagnosisDentistMismatch, result.Error);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(VisitStatuses.Created)]
    [InlineData(VisitStatuses.Completed)]
    [InlineData(VisitStatuses.Cancelled)]
    public async Task UpdateDiagnosisAsync_NotInProgressVisit_ReturnsVisitNotInProgressError(string status)
    {
        var visit = new Visit
        {
            VisitId = 20,
            PatientId = 7,
            DentistId = 12,
            Status = status,
        };
        _visitRepository.GetForUpdateAsync(20, Arg.Any<CancellationToken>()).Returns(visit);

        var request = new UpdateVisitDiagnosisRequest("Sâu răng", null);
        var result = await CreateService().UpdateDiagnosisAsync(
            20, request, 12, RoleCodes.Dentist, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.DiagnosisVisitNotInProgress, result.Error);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateDiagnosisAsync_VisitNotFound_ReturnsNotFoundError()
    {
        _visitRepository.GetForUpdateAsync(404, Arg.Any<CancellationToken>()).Returns((Visit?)null);

        var request = new UpdateVisitDiagnosisRequest("Sâu răng", null);
        var result = await CreateService().UpdateDiagnosisAsync(
            404, request, 12, RoleCodes.Dentist, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound, result.Error);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateVisitDiagnosisRequestValidator_ValidatesExpectedRules()
    {
        var validator = new UpdateVisitDiagnosisRequestValidator();

        var emptyDiagnosis = await validator.ValidateAsync(new UpdateVisitDiagnosisRequest(""));
        Assert.False(emptyDiagnosis.IsValid);
        Assert.Contains(emptyDiagnosis.Errors, e => e.PropertyName == nameof(UpdateVisitDiagnosisRequest.Diagnosis));

        var tooLongDiagnosis = await validator.ValidateAsync(new UpdateVisitDiagnosisRequest(new string('a', 1001)));
        Assert.False(tooLongDiagnosis.IsValid);

        var tooLongNotes = await validator.ValidateAsync(new UpdateVisitDiagnosisRequest("Sâu răng", new string('b', 2001)));
        Assert.False(tooLongNotes.IsValid);

        var valid = await validator.ValidateAsync(new UpdateVisitDiagnosisRequest("Sâu răng", "Theo dõi"));
        Assert.True(valid.IsValid);
    }

    [Fact]
    public async Task GetVisitByIdAsync_ProtectsPatientIdorAndReturnsSuccess()
    {
        var visit = new Visit
        {
            VisitId = 10,
            PatientId = 5,
            Status = VisitStatuses.Completed,
            Diagnosis = "Viêm nướu",
        };
        _visitRepository.GetByIdAsync(10, Arg.Any<CancellationToken>()).Returns(visit);
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 5, UserId = 100 });

        var service = CreateService();

        // Matching patient
        var successResult = await service.GetVisitByIdAsync(10, 100, RoleCodes.Patient);
        Assert.True(successResult.IsSuccess);
        Assert.Equal("Viêm nướu", successResult.Value!.Diagnosis);

        // Mismatched patient -> NotFound
        var idorResult = await service.GetVisitByIdAsync(10, 999, RoleCodes.Patient);
        Assert.True(idorResult.IsFailure);
        Assert.Equal(Error.NotFound, idorResult.Error);

        // Staff (e.g. Dentist) -> success without matching patient UserId
        var staffResult = await service.GetVisitByIdAsync(10, 12, RoleCodes.Dentist);
        Assert.True(staffResult.IsSuccess);
    }
}
