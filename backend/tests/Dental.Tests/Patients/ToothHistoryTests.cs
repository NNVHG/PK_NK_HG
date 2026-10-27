using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Features.MOD_FDI.Services;
using Dental.Application.Features.MOD_FDI.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;
namespace Dental.Tests.Patients;
public sealed class ToothHistoryTests
{
    [Theory] [InlineData(11)] [InlineData(18)] [InlineData(28)] [InlineData(48)] [InlineData(51)] [InlineData(85)] [InlineData(null)]
    public void ValidFdiFilter(int? tooth) => Assert.True(new ToothHistoryRequestValidator().Validate(new ToothHistoryRequest(1, tooth)).IsValid);
    [Theory] [InlineData(0)] [InlineData(19)] [InlineData(49)] [InlineData(56)] [InlineData(86)] [InlineData(99)]
    public void InvalidFdiFilter(int tooth) => Assert.False(new ToothHistoryRequestValidator().Validate(new ToothHistoryRequest(1, tooth)).IsValid);
    [Theory] [InlineData(0, 1, 20)] [InlineData(1, 0, 20)] [InlineData(1, 1000001, 20)] [InlineData(1, 1, 0)] [InlineData(1, 1, 51)]
    public void InvalidPaginationOrPatient(int patient, int page, int size) => Assert.False(new ToothHistoryRequestValidator().Validate(new ToothHistoryRequest(patient, 11, page, size)).IsValid);
    [Theory] [InlineData("UNKNOWN", 1)] [InlineData(null, 1)] [InlineData(RoleCodes.Admin, 0)]
    public async Task RejectInvalidActorBeforeReading(string? role, int actor)
    {
        var patients = Substitute.For<IPatientRepository>(); var repo = Substitute.For<IToothHistoryRepository>();
        Assert.Equal(Error.Forbidden, (await new ToothHistoryService(patients, repo, new()).GetAsync(new(1, 11), actor, role)).Error);
        Assert.Empty(patients.ReceivedCalls()); Assert.Empty(repo.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)]
    public async Task AllowedRolesReturnRepositoryResult(string role)
    {
        var patients = Substitute.For<IPatientRepository>(); var repo = Substitute.For<IToothHistoryRepository>();
        patients.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 1, UserId = 7 });
        var request = new ToothHistoryRequest(1, 51, 2, 20); var page = PagedResult<ToothHistoryItemResponse>.Create([], 0, 2, 20);
        repo.GetAsync(request, Arg.Any<CancellationToken>()).Returns(page);
        Assert.Same(page, (await new ToothHistoryService(patients, repo, new()).GetAsync(request, 7, role)).Value);
    }
    [Theory] [InlineData(null)] [InlineData(8)]
    public async Task PatientCannotReadUnownedProfiles(int? owner)
    {
        var patients = Substitute.For<IPatientRepository>(); var repo = Substitute.For<IToothHistoryRepository>();
        patients.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 1, UserId = owner });
        Assert.Equal(Error.NotFound, (await new ToothHistoryService(patients, repo, new()).GetAsync(new(1), 7, RoleCodes.Patient)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Fact]
    public async Task GuardianReadsWardHistoryOnlyWhileRelationIsActive()
    {
        var patients = Substitute.For<IPatientRepository>();
        var guardians = Substitute.For<IPatientGuardianRepository>();
        var repo = Substitute.For<IToothHistoryRepository>();
        var request = new ToothHistoryRequest(12);
        var page = PagedResult<ToothHistoryItemResponse>.Create([], 0, 1, 20);
        patients.GetByIdAsync(12, Arg.Any<CancellationToken>()).Returns(new Patient { PatientId = 12 });
        guardians.HasActiveGuardianshipAsync(7, 12, Arg.Any<CancellationToken>()).Returns(true, false);
        repo.GetAsync(request, Arg.Any<CancellationToken>()).Returns(page);
        var service = new ToothHistoryService(patients, repo, new(), guardians);

        Assert.Same(page, (await service.GetAsync(request, 7, RoleCodes.Patient)).Value);
        Assert.Equal(Error.NotFound, (await service.GetAsync(request, 7, RoleCodes.Patient)).Error);
        await repo.Received(1).GetAsync(request, Arg.Any<CancellationToken>());
    }
    [Fact] public async Task MissingPatientReturnsNotFound()
    {
        var repo = Substitute.For<IToothHistoryRepository>();
        Assert.Equal(Error.NotFound, (await new ToothHistoryService(Substitute.For<IPatientRepository>(), repo, new()).GetAsync(new(1), 7, RoleCodes.Admin)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task InvalidRequestDoesNotReadPatient()
    {
        var patients = Substitute.For<IPatientRepository>();
        Assert.Equal(Error.Validation, (await new ToothHistoryService(patients, Substitute.For<IToothHistoryRepository>(), new()).GetAsync(new(1, 99), 7, RoleCodes.Admin)).Error);
        Assert.Empty(patients.ReceivedCalls());
    }
}
