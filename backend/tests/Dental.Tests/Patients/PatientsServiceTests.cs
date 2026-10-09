using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Errors;
using Dental.Application.Features.Patients.Services;
using Dental.Application.Features.Patients.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Patients;

public sealed class PatientsServiceTests
{
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private PatientsService CreateService() => new(_patientRepository, _unitOfWork, _auditLogger);

    private static CreatePatientRequest ValidRequest(bool confirmNotDuplicate = false)
        => new("Nguyễn An", new DateOnly(1990, 2, 3), "Female", "0912345678", "an@example.com", "Địa chỉ thử nghiệm", confirmNotDuplicate);

    [Fact]
    public async Task GetMyActiveProfilesAsync_ReturnsOnlyProfilesProvidedByCurrentUserRepository()
    {
        var profiles = new[]
        {
            new Patient { PatientId = 4, PatientNumber = 40, FullName = "An", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0900000000" },
            new Patient { PatientId = 5, PatientNumber = 50, FullName = "Bình", DateOfBirth = new DateOnly(2010, 2, 2), Phone = "0900000000" },
        };
        _patientRepository.GetActiveByUserIdAsync(22, Arg.Any<CancellationToken>()).Returns(profiles);

        var result = await CreateService().GetMyActiveProfilesAsync(22);

        Assert.True(result.IsSuccess);
        Assert.Collection(result.Value!,
            item => Assert.Equal((4, "An"), (item.PatientId, item.FullName)),
            item => Assert.Equal((5, "Bình"), (item.PatientId, item.FullName)));
        await _patientRepository.Received(1).GetActiveByUserIdAsync(22, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePatientAsync_ValidRequest_CreatesPatientAndAuditsWithoutPersonalData()
    {
        _patientRepository.FindPossibleDuplicatesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Patient>());
        _patientRepository.AddAsync(Arg.Any<Patient>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var patient = call.Arg<Patient>();
            patient.PatientId = 21;
            patient.PatientNumber = 42;
            return Task.CompletedTask;
        });

        var result = await CreateService().CreatePatientAsync(7, ValidRequest(), "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal("BN000042", result.Value!.PatientCode);
        Assert.Equal("Nguyễn An", result.Value.FullName);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.PatientCreated,
            entityType: "Patient",
            entityId: 21,
            userId: 7,
            detail: Arg.Is<string?>(detail =>
                detail != null && detail.Contains("createdByUserId") && detail.Contains("patientCode") &&
                detail.Contains("confirmedNotDuplicate") && !detail.Contains("Nguyễn An") &&
                !detail.Contains("0912345678") && !detail.Contains("1990-02-03") && !detail.Contains("an@example.com")),
            ipAddress: "127.0.0.1",
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePatientAsync_PossibleDuplicateWithoutConfirmation_ReturnsDuplicateErrorAndDoesNotSave()
    {
        _patientRepository.FindPossibleDuplicatesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new Patient
                {
                    PatientNumber = 8,
                    FullName = "Nguyễn An",
                    DateOfBirth = new DateOnly(1990, 2, 3),
                    Phone = "0912345678",
                },
            });

        var result = await CreateService().CreatePatientAsync(7, ValidRequest(), null);

        Assert.True(result.IsFailure);
        var duplicateError = Assert.IsType<PatientPossibleDuplicateError>(result.Error);
        Assert.Equal("PATIENT_POSSIBLE_DUPLICATE", duplicateError.Code);
        Assert.Equal("09*****678", duplicateError.Duplicates.Single().Phone);
        Assert.Equal("Nguyễn An", duplicateError.Duplicates.Single().FullName);
        await _patientRepository.DidNotReceive().AddAsync(Arg.Any<Patient>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePatientAsync_ConfirmedPossibleDuplicate_CreatesSeparateRecordWithoutMerging()
    {
        var existingPatient = new Patient
        {
            PatientId = 4,
            PatientNumber = 4,
            FullName = "Nguyễn An",
            DateOfBirth = new DateOnly(1990, 2, 3),
            Phone = "0912345678",
            UserId = 99,
        };
        _patientRepository.FindPossibleDuplicatesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new[] { existingPatient });
        _patientRepository.AddAsync(Arg.Any<Patient>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var patient = call.Arg<Patient>();
            patient.PatientId = 21;
            patient.PatientNumber = 42;
            return Task.CompletedTask;
        });

        var result = await CreateService().CreatePatientAsync(7, ValidRequest(confirmNotDuplicate: true), null);

        Assert.True(result.IsSuccess);
        Assert.Equal(21, result.Value!.PatientId);
        Assert.Equal(99, existingPatient.UserId);
        await _patientRepository.Received(1).AddAsync(
            Arg.Is<Patient>(patient => patient.PatientId == 21 && patient.PatientNumber == 42),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPatientByIdAsync_PatientRequestsAnotherUsersRecord_ReturnsNotFound()
    {
        _patientRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 3, PatientNumber = 3, UserId = 20 });

        var result = await CreateService().GetPatientByIdAsync(3, 10, RoleCodes.Patient);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound, result.Error);
    }

    [Theory]
    [InlineData("0912345678")]
    [InlineData("BN000042")]
    [InlineData("Nguyen An")]
    public async Task GetPatientsAsync_ForwardsPhoneCodeAndNameKeywords(string keyword)
    {
        _patientRepository.SearchAsync(keyword, 1, 20, Arg.Any<CancellationToken>())
            .Returns(PagedResult<Patient>.Create(
                [new Patient
                {
                    PatientNumber = 42,
                    FullName = "Nguyễn An",
                    DateOfBirth = new DateOnly(1990, 2, 3),
                    Gender = "Female",
                    Phone = "0912345678",
                    Email = "private@example.com",
                    Address = "Địa chỉ riêng",
                }],
                1,
                1,
                20));

        var result = await CreateService().GetPatientsAsync(new PatientQueryRequest
        {
            Keyword = keyword,
            Page = 1,
            PageSize = 20,
        });

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("BN000042", item.PatientCode);
        Assert.Equal("Nguyễn An", item.FullName);
        Assert.Equal("0912345678", item.Phone);
        var serialized = System.Text.Json.JsonSerializer.Serialize(item);
        Assert.DoesNotContain("private@example.com", serialized);
        Assert.DoesNotContain("Địa chỉ riêng", serialized);
        await _patientRepository.Received(1).SearchAsync(keyword, 1, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPatientsAsync_EmptyKeyword_ReturnsPagedResult()
    {
        _patientRepository.SearchAsync(string.Empty, 2, 10, Arg.Any<CancellationToken>())
            .Returns(PagedResult<Patient>.Create(
                [new Patient { PatientNumber = 8, FullName = "Bệnh nhân thử", Phone = "0900000000" }],
                12,
                2,
                10));

        var result = await CreateService().GetPatientsAsync(new PatientQueryRequest
        {
            Keyword = string.Empty,
            Page = 2,
            PageSize = 10,
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(10, result.Value.PageSize);
        await _patientRepository.Received(1).SearchAsync(string.Empty, 2, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePatientAsync_ValidRequest_AuditsOnlyChangedFieldNames()
    {
        var patient = new Patient
        {
            PatientId = 6,
            PatientNumber = 6,
            FullName = "Tên cũ",
            DateOfBirth = new DateOnly(1990, 2, 3),
            Gender = "Female",
            Phone = "0912345678",
            Email = null,
            Address = null,
        };
        _patientRepository.GetByIdAsync(6, Arg.Any<CancellationToken>()).Returns(patient);
        _patientRepository.FindPossibleDuplicatesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>(), 6)
            .Returns(Array.Empty<Patient>());

        var result = await CreateService().UpdatePatientAsync(
            7,
            6,
            new UpdatePatientRequest(
                "Tên mới",
                new DateOnly(1990, 2, 3),
                "Female",
                "0912345678",
                "new@example.com",
                null),
            "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal("BN000006", result.Value!.PatientCode);
        Assert.Equal("Tên mới", result.Value.FullName);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.PatientUpdated,
            entityType: "Patient",
            entityId: 6,
            userId: 7,
            detail: Arg.Is<string?>(detail =>
                detail != null && detail.Contains("FullName") && detail.Contains("Email") &&
                !detail.Contains("Tên mới") && !detail.Contains("new@example.com") && !detail.Contains("0912345678")),
            ipAddress: "127.0.0.1",
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePatientAsync_PossibleDuplicateWithoutConfirmation_DoesNotSaveChanges()
    {
        var patient = new Patient
        {
            PatientId = 6,
            PatientNumber = 6,
            FullName = "Tên cũ",
            DateOfBirth = new DateOnly(1990, 2, 3),
            Phone = "0912345678",
        };
        _patientRepository.GetByIdAsync(6, Arg.Any<CancellationToken>()).Returns(patient);
        _patientRepository.FindPossibleDuplicatesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>(), 6)
            .Returns(new[]
            {
                new Patient
                {
                    PatientId = 8,
                    PatientNumber = 8,
                    FullName = "Tên khác",
                    DateOfBirth = new DateOnly(1990, 2, 3),
                    Phone = "0900000000",
                },
            });

        var result = await CreateService().UpdatePatientAsync(
            7,
            6,
            new UpdatePatientRequest("Tên mới", new DateOnly(1990, 2, 3), null, "0912345678", null, null),
            null);

        Assert.True(result.IsFailure);
        Assert.IsType<PatientPossibleDuplicateError>(result.Error);
        Assert.Equal("Tên cũ", patient.FullName);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLogger.DidNotReceive().LogAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePatientRequestValidator_InvalidFields_AreRejected()
    {
        var vietnamClock = new VietnamClock(TimeProvider.System);
        var validator = new CreatePatientRequestValidator(vietnamClock);
        var request = new CreatePatientRequest(
            " ",
            vietnamClock.Today.AddDays(1),
            "Unknown",
            "123",
            "email-khong-hop-le");

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.FullName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.DateOfBirth));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.Gender));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.Phone));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.Email));
    }

    [Fact]
    public async Task PatientQueryRequestValidator_RejectsPageSizeAboveFifty()
    {
        var validator = new PatientQueryRequestValidator();
        var result = await validator.ValidateAsync(new PatientQueryRequest { Page = 1, PageSize = 51 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(PatientQueryRequest.PageSize));
    }

    [Fact]
    public void UpdatePatientRequest_DoesNotExposeUserIdOrPatientCode()
    {
        var properties = typeof(UpdatePatientRequest).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain(nameof(Patient.UserId), properties);
        Assert.DoesNotContain(nameof(Patient.PatientCode), properties);
    }
}
