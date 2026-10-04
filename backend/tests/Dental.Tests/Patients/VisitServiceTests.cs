using Dental.Application.Common;
using Dental.Application.Features.Visits.DTOs;
using Dental.Application.Features.Visits.Services;
using Dental.Application.Features.Visits.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

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
}
