using Dental.Application.Common;
using Dental.Application.Features.MedicalHistory.DTOs;
using Dental.Application.Features.MedicalHistory.Services;
using Dental.Application.Features.MedicalHistory.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Patients;

public sealed class MedicalHistoryServiceTests
{
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly IVisitRepository _visitRepository = Substitute.For<IVisitRepository>();
    private readonly IMedicalHistoryRepository _medicalHistoryRepository = Substitute.For<IMedicalHistoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private MedicalHistoryService CreateService()
        => new(_patientRepository, _visitRepository, _medicalHistoryRepository, _unitOfWork, _auditLogger);

    [Fact]
    public async Task RecordAsync_CreatesFirstSnapshot()
    {
        var visit = CreateVisit();
        _visitRepository.GetByIdAsync(visit.VisitId, Arg.Any<CancellationToken>()).Returns(visit);
        _medicalHistoryRepository.AddAsync(Arg.Any<MedicalHistoryRecord>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var record = call.Arg<MedicalHistoryRecord>();
                record.RecordId = 31;
                record.Items.Single().ItemId = 41;
                return Task.CompletedTask;
            });

        var result = await CreateService().RecordAsync(visit.VisitId, 9, CreateRequest("Penicillin"), "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.PatientId);
        Assert.Equal(visit.VisitId, result.Value.VisitId);
        Assert.Equal("Penicillin", result.Value.Items.Single().Name);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.MedicalHistoryRecorded,
            entityType: "MedicalHistoryRecord",
            entityId: 31,
            userId: 9,
            detail: Arg.Is<string?>(detail => detail != null && detail.Contains("visitId") && detail.Contains("itemCount")),
            ipAddress: "127.0.0.1",
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_SecondSnapshotPreservesFirstAndLatestReturnsSecond()
    {
        var visit = CreateVisit();
        var records = new List<MedicalHistoryRecord>();
        _visitRepository.GetByIdAsync(visit.VisitId, Arg.Any<CancellationToken>()).Returns(visit);
        _patientRepository.GetByIdAsync(visit.PatientId, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = visit.PatientId });
        _medicalHistoryRepository.AddAsync(Arg.Any<MedicalHistoryRecord>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var record = call.Arg<MedicalHistoryRecord>();
                record.RecordId = records.Count + 1;
                records.Add(record);
                return Task.CompletedTask;
            });
        _medicalHistoryRepository.GetLatestByPatientIdAsync(visit.PatientId, Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(records.OrderByDescending(record => record.RecordId).FirstOrDefault()));

        await CreateService().RecordAsync(visit.VisitId, 9, CreateRequest("Dị ứng phấn hoa"), null);
        await CreateService().RecordAsync(visit.VisitId, 9, CreateRequest("Hen suyễn"), null);
        var latest = await CreateService().GetLatestAsync(visit.PatientId, 77, RoleCodes.Admin);

        Assert.Equal(2, records.Count);
        Assert.Equal("Dị ứng phấn hoa", records[0].Items.Single().Name);
        Assert.Equal("Hen suyễn", records[1].Items.Single().Name);
        Assert.True(latest.IsSuccess);
        Assert.Equal("Hen suyễn", latest.Value!.Items.Single().Name);
    }

    [Theory]
    [InlineData(VisitStatuses.Completed)]
    [InlineData(VisitStatuses.Cancelled)]
    public async Task RecordAsync_ClosedVisit_IsRejected(string status)
    {
        var visit = CreateVisit();
        visit.Status = status;
        _visitRepository.GetByIdAsync(visit.VisitId, Arg.Any<CancellationToken>()).Returns(visit);

        var result = await CreateService().RecordAsync(visit.VisitId, 9, CreateRequest("Dị ứng"), null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.MedicalHistoryVisitClosed, result.Error);
        await _medicalHistoryRepository.DidNotReceive().AddAsync(
            Arg.Any<MedicalHistoryRecord>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLatestAsync_PatientCannotReadAnotherPatientsHistory_ReturnsNotFound()
    {
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5, UserId = 22 });

        var result = await CreateService().GetLatestAsync(5, 99, RoleCodes.Patient);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound, result.Error);
        await _medicalHistoryRepository.DidNotReceive().GetLatestByPatientIdAsync(
            Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_AuditContainsNoMedicalItemName()
    {
        var visit = CreateVisit();
        string? auditDetail = null;
        _visitRepository.GetByIdAsync(visit.VisitId, Arg.Any<CancellationToken>()).Returns(visit);
        _medicalHistoryRepository.AddAsync(Arg.Any<MedicalHistoryRecord>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _auditLogger.LogAsync(
            action: Arg.Any<string>(), entityType: Arg.Any<string>(), entityId: Arg.Any<int?>(),
            userId: Arg.Any<int?>(), detail: Arg.Do<string?>(value => auditDetail = value),
            ipAddress: Arg.Any<string?>(), ct: Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await CreateService().RecordAsync(visit.VisitId, 9, CreateRequest("Thông tin y tế nhạy cảm"), null);

        Assert.NotNull(auditDetail);
        Assert.Contains("visitId", auditDetail);
        Assert.Contains("itemCount", auditDetail);
        Assert.DoesNotContain("Thông tin y tế nhạy cảm", auditDetail);
    }

    [Fact]
    public async Task RecordMedicalHistoryRequestValidator_RejectsDuplicateNamesIgnoringCaseAndOver50Items()
    {
        var validator = new RecordMedicalHistoryRequestValidator();
        var duplicateResult = await validator.ValidateAsync(new RecordMedicalHistoryRequest
        {
            Items = [
                new(MedicalHistoryTypes.Allergy, "Penicillin", false, null),
                new(MedicalHistoryTypes.Condition, " penicillin ", false, null),
            ],
        });
        var tooManyResult = await validator.ValidateAsync(new RecordMedicalHistoryRequest
        {
            Items = Enumerable.Range(1, 51)
                .Select(index => new MedicalHistoryItemRequest(MedicalHistoryTypes.Condition, $"Mục {index}", false, null))
                .ToList(),
        });

        Assert.Contains(duplicateResult.Errors, error => error.ErrorMessage.Contains("không được trùng"));
        Assert.Contains(tooManyResult.Errors, error => error.ErrorMessage.Contains("tối đa 50"));
    }

    private static Visit CreateVisit() => new()
    {
        VisitId = 7,
        PatientId = 5,
        Status = VisitStatuses.InProgress,
        CreatedByUserId = 3,
    };

    private static RecordMedicalHistoryRequest CreateRequest(string name) => new()
    {
        Items = [new(MedicalHistoryTypes.Allergy, name, true, null)],
    };
}
