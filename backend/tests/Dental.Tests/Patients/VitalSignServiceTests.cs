using Dental.Application.Common;
using Dental.Application.Features.VitalSigns.DTOs;
using Dental.Application.Features.VitalSigns.Services;
using Dental.Application.Features.VitalSigns.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Patients;

public sealed class VitalSignServiceTests
{
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly IVisitRepository _visitRepository = Substitute.For<IVisitRepository>();
    private readonly IVitalSignRepository _vitalSignRepository = Substitute.For<IVitalSignRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private VitalSignService CreateService()
        => new(_patientRepository, _visitRepository, _vitalSignRepository, _unitOfWork, _auditLogger);

    [Fact]
    public async Task RecordAsync_RecordsVitalSignsAndAuditsOnlyVisitId()
    {
        var visit = CreateVisit();
        string? auditDetail = null;
        _visitRepository.GetByIdAsync(visit.VisitId, Arg.Any<CancellationToken>()).Returns(visit);
        _vitalSignRepository.AddAsync(Arg.Any<VitalSignRecord>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<VitalSignRecord>().VitalSignRecordId = 31;
                return Task.CompletedTask;
            });
        _auditLogger.LogAsync(
            action: Arg.Any<string>(), entityType: Arg.Any<string>(), entityId: Arg.Any<int?>(),
            userId: Arg.Any<int?>(), detail: Arg.Do<string?>(value => auditDetail = value),
            ipAddress: Arg.Any<string?>(), ct: Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var request = new RecordVitalSignsRequest
        {
            SystolicBp = 120,
            DiastolicBp = 80,
            PulseBpm = 72,
            TemperatureC = 36.6m,
        };
        var result = await CreateService().RecordAsync(visit.VisitId, 9, request, "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(visit.PatientId, result.Value!.PatientId);
        Assert.Equal(visit.VisitId, result.Value.VisitId);
        Assert.Equal(120, result.Value.SystolicBp);
        Assert.Equal(80, result.Value.DiastolicBp);
        Assert.Equal(72, result.Value.PulseBpm);
        Assert.Equal(36.6m, result.Value.TemperatureC);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.Equal("{\"visitId\":7}", auditDetail);
        Assert.DoesNotContain("120", auditDetail);
        Assert.DoesNotContain("80", auditDetail);
        Assert.DoesNotContain("72", auditDetail);
        Assert.DoesNotContain("36.6", auditDetail);
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.VitalSignsRecorded,
            entityType: "VitalSignRecord",
            entityId: 31,
            userId: 9,
            detail: Arg.Any<string?>(),
            ipAddress: "127.0.0.1",
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_SecondRecordPreservesFirstRecord()
    {
        var visit = CreateVisit();
        var records = new List<VitalSignRecord>();
        _visitRepository.GetByIdAsync(visit.VisitId, Arg.Any<CancellationToken>()).Returns(visit);
        _vitalSignRepository.AddAsync(Arg.Any<VitalSignRecord>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var record = call.Arg<VitalSignRecord>();
                record.VitalSignRecordId = records.Count + 1;
                records.Add(record);
                return Task.CompletedTask;
            });
        _vitalSignRepository.GetPatientRecordsAsync(visit.PatientId, 1, 20, Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(PagedResult<VitalSignRecord>.Create(
                records.OrderByDescending(record => record.VitalSignRecordId).ToList(),
                records.Count,
                1,
                20)));
        _patientRepository.GetByIdAsync(visit.PatientId, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = visit.PatientId });

        await CreateService().RecordAsync(visit.VisitId, 9, new RecordVitalSignsRequest { PulseBpm = 70 }, null);
        await CreateService().RecordAsync(visit.VisitId, 9, new RecordVitalSignsRequest { PulseBpm = 74 }, null);
        var result = await CreateService().GetPatientRecordsAsync(
            visit.PatientId, 9, RoleCodes.Admin, new VitalSignQueryRequest());

        Assert.Equal(2, records.Count);
        Assert.Equal(70, records.Single(record => record.VitalSignRecordId == 1).PulseBpm);
        Assert.Equal(74, records.Single(record => record.VitalSignRecordId == 2).PulseBpm);
        Assert.True(result.IsSuccess);
        Assert.Equal(new int?[] { 74, 70 }, result.Value!.Items.Select(record => record.PulseBpm));
    }

    [Theory]
    [InlineData(VisitStatuses.Completed)]
    [InlineData(VisitStatuses.Cancelled)]
    public async Task RecordAsync_ClosedVisitIsRejected(string status)
    {
        var visit = CreateVisit();
        visit.Status = status;
        _visitRepository.GetByIdAsync(visit.VisitId, Arg.Any<CancellationToken>()).Returns(visit);

        var result = await CreateService().RecordAsync(
            visit.VisitId, 9, new RecordVitalSignsRequest { PulseBpm = 70 }, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.VitalSignsVisitClosed, result.Error);
        await _vitalSignRepository.DidNotReceive().AddAsync(
            Arg.Any<VitalSignRecord>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_MissingVisitReturnsNotFound()
    {
        var result = await CreateService().RecordAsync(
            404, 9, new RecordVitalSignsRequest { PulseBpm = 70 }, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound, result.Error);
        await _vitalSignRepository.DidNotReceive().AddAsync(
            Arg.Any<VitalSignRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPatientRecordsAsync_PatientCannotReadAnotherPatientsRecords()
    {
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5, UserId = 22 });

        var result = await CreateService().GetPatientRecordsAsync(
            5, 99, RoleCodes.Patient, new VitalSignQueryRequest());

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound, result.Error);
        await _vitalSignRepository.DidNotReceive().GetPatientRecordsAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordVitalSignsRequestValidator_RejectsWhenAllMeasurementsAreMissing()
    {
        var result = await new RecordVitalSignsRequestValidator()
            .ValidateAsync(new RecordVitalSignsRequest());

        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("ít nhất một chỉ số"));
    }

    [Theory]
    [InlineData(49, null, null, null)]
    [InlineData(null, 161, null, null)]
    [InlineData(null, null, 19, null)]
    [InlineData(null, null, null, 44)]
    public async Task RecordVitalSignsRequestValidator_RejectsMeasurementsOutsideAllowedRanges(
        int? systolic,
        int? diastolic,
        int? pulse,
        int? temperature)
    {
        var result = await new RecordVitalSignsRequestValidator().ValidateAsync(new RecordVitalSignsRequest
        {
            SystolicBp = systolic,
            DiastolicBp = diastolic,
            PulseBpm = pulse,
            TemperatureC = temperature,
        });

        Assert.NotEmpty(result.Errors);
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(90, 100)]
    public async Task RecordVitalSignsRequestValidator_RequiresSystolicAboveDiastolic(
        int systolic,
        int diastolic)
    {
        var result = await new RecordVitalSignsRequestValidator().ValidateAsync(new RecordVitalSignsRequest
        {
            SystolicBp = systolic,
            DiastolicBp = diastolic,
        });

        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("tâm thu phải lớn hơn"));
    }

    private static Visit CreateVisit() => new()
    {
        VisitId = 7,
        PatientId = 5,
        Status = VisitStatuses.InProgress,
        CreatedByUserId = 3,
    };
}
