using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Services;
using Dental.Application.Features.Patients.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Patients;

public sealed class PatientGuardianServiceTests
{
    private readonly IPatientGuardianRepository _repository = Substitute.For<IPatientGuardianRepository>();
    private readonly AssignPatientGuardianRequestValidator _assignValidator = new();
    private readonly RevokePatientGuardianRequestValidator _revokeValidator = new();
    private readonly PromoteWardRequestValidator _promoteValidator = new();

    private PatientGuardianService CreateService()
        => new(_repository, _assignValidator, _revokeValidator, _promoteValidator);

    [Fact]
    public async Task AssignGuardian_WhenActorIsPatient_ReturnsForbidden()
    {
        var request = new AssignPatientGuardianRequest(1, 2, "Cha");
        var result = await CreateService().AssignGuardianAsync(request, 10, RoleCodes.Patient, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.Forbidden, result.Error);
    }

    [Fact]
    public async Task AssignGuardian_WhenGuardianSameAsWard_ReturnsValidationError()
    {
        var request = new AssignPatientGuardianRequest(5, 5, "Cha");
        var result = await CreateService().AssignGuardianAsync(request, 1, RoleCodes.Receptionist, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.Validation.Code, result.Error.Code);
    }

    [Fact]
    public async Task AssignGuardian_WhenValid_CallsRepository()
    {
        var request = new AssignPatientGuardianRequest(1, 2, "Mẹ", "Sổ hộ khẩu số 123");
        var expectedResponse = new PatientGuardianResponse(
            100, 1, "BN000001", "Mẹ A", 2, "BN000002", "Con B", "Mẹ", true, DateTime.UtcNow, 1, "Lễ tân", null, null, null, "Sổ hộ khẩu số 123");

        _repository.AssignGuardianAsync(request, 1, null, Arg.Any<CancellationToken>())
            .Returns(Result<PatientGuardianResponse>.Success(expectedResponse));

        var result = await CreateService().AssignGuardianAsync(request, 1, RoleCodes.Receptionist, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value!.PatientGuardianId);
    }

    [Fact]
    public async Task RevokeGuardian_WhenActorIsStaff_CallsRepository()
    {
        var request = new RevokePatientGuardianRequest(100, DateTime.UtcNow, "Đổi người giám hộ");
        var expectedResponse = new PatientGuardianResponse(
            100, 1, "BN000001", "Mẹ A", 2, "BN000002", "Con B", "Mẹ", false, DateTime.UtcNow, 1, "Lễ tân", DateTime.UtcNow, 1, "Lễ tân", "Đổi người giám hộ");

        _repository.RevokeGuardianAsync(2, 1, request, null, Arg.Any<CancellationToken>())
            .Returns(Result<PatientGuardianResponse>.Success(expectedResponse));

        var result = await CreateService().RevokeGuardianAsync(2, request, 1, RoleCodes.Admin, null);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
    }

    [Fact]
    public async Task PromoteWardToIndependentPatient_WhenInvalidCitizenId_ReturnsValidationError()
    {
        var request = new PromoteWardRequest("12345"); // Invalid CCCD
        var result = await CreateService().PromoteWardToIndependentPatientAsync(2, request, 1, RoleCodes.Receptionist, null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.Validation.Code, result.Error.Code);
    }

    [Fact]
    public async Task PromoteWardToIndependentPatient_WhenValid_CallsRepository()
    {
        var request = new PromoteWardRequest("079090123456", "0912345678");
        var expectedPatient = new PatientResponse(
            2, "BN000002", "Con B", new DateOnly(2008, 1, 1), "Male", "0912345678", null, null, true);

        _repository.PromoteWardToIndependentPatientAsync(2, request, 1, null, Arg.Any<CancellationToken>())
            .Returns(Result<PatientResponse>.Success(expectedPatient));

        var result = await CreateService().PromoteWardToIndependentPatientAsync(2, request, 1, RoleCodes.Receptionist, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("BN000002", result.Value!.PatientCode);
    }

    [Fact]
    public async Task GetMyWardsAsync_WhenPatient_ReturnsListOfWards()
    {
        var wardRecord = new PatientGuardian
        {
            PatientGuardianId = 1,
            GuardianPatientId = 10,
            WardPatientId = 20,
            Relationship = "Mẹ",
            IsActive = true,
            AssignedAt = DateTime.UtcNow,
            WardPatient = new Patient
            {
                PatientId = 20,
                PatientNumber = 20,
                FullName = "Bé C",
                DateOfBirth = new DateOnly(2018, 5, 5),
                Gender = "Female",
                IsActive = true
            }
        };

        _repository.GetActiveByGuardianUserIdAsync(55, Arg.Any<CancellationToken>())
            .Returns(new List<PatientGuardian> { wardRecord });

        var result = await CreateService().GetMyWardsAsync(55, RoleCodes.Patient);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(20, result.Value![0].WardPatientId);
        Assert.Equal("Bé C", result.Value![0].FullName);
    }
}
