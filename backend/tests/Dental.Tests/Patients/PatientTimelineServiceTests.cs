using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Patients;

public sealed class PatientTimelineServiceTests
{
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly IVisitRepository _visitRepository = Substitute.For<IVisitRepository>();

    private PatientTimelineService CreateService()
        => new(_patientRepository, _visitRepository);

    [Fact]
    public async Task GetAsync_ReturnsNewestFirstTimelinePageAndFlags()
    {
        var newest = new PatientTimelineItemResponse(20, VisitStatuses.Completed, null, null, "Nha sĩ A", true, true);
        var older = new PatientTimelineItemResponse(10, VisitStatuses.Completed, null, null, null, false, true);
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5 });
        _visitRepository.GetPatientTimelineAsync(5, 1, 20, Arg.Any<CancellationToken>())
            .Returns(PagedResult<PatientTimelineItemResponse>.Create([newest, older], 2, 1, 20));

        var result = await CreateService().GetAsync(5, 7, RoleCodes.Admin, new PatientTimelineQueryRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 20, 10 }, result.Value!.Items.Select(item => item.VisitId));
        Assert.True(result.Value.Items[0].HasMedicalHistory);
        Assert.True(result.Value.Items[0].HasVitalSigns);
        Assert.False(result.Value.Items[1].HasMedicalHistory);
        Assert.True(result.Value.Items[1].HasVitalSigns);
    }

    [Fact]
    public async Task GetAsync_ForwardsPageAndPageSizeToRepository()
    {
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5 });
        _visitRepository.GetPatientTimelineAsync(5, 2, 5, Arg.Any<CancellationToken>())
            .Returns(PagedResult<PatientTimelineItemResponse>.Create([], 12, 2, 5));

        var result = await CreateService().GetAsync(
            5, 7, RoleCodes.Receptionist, new PatientTimelineQueryRequest { Page = 2, PageSize = 5 });

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(5, result.Value.PageSize);
        await _visitRepository.Received(1).GetPatientTimelineAsync(
            5, 2, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_PatientCannotReadAnotherPatientsTimeline_ReturnsNotFound()
    {
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5, UserId = 22 });

        var result = await CreateService().GetAsync(
            5, 99, RoleCodes.Patient, new PatientTimelineQueryRequest());

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NotFound, result.Error);
        await _visitRepository.DidNotReceive().GetPatientTimelineAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
