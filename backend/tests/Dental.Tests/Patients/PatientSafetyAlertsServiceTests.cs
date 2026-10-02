using Dental.Application.Features.Patients.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Patients;

public sealed class PatientSafetyAlertsServiceTests
{
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly IMedicalHistoryRepository _medicalHistoryRepository = Substitute.For<IMedicalHistoryRepository>();

    private PatientSafetyAlertsService CreateService()
        => new(_patientRepository, _medicalHistoryRepository);

    [Fact]
    public async Task GetAsync_WhenPatientHasNoHistory_ReturnsFalseAndEmptyAlerts()
    {
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5 });
        _medicalHistoryRepository.GetLatestByPatientIdAsync(5, Arg.Any<CancellationToken>())
            .Returns((MedicalHistoryRecord?)null);

        var result = await CreateService().GetAsync(5);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.HasHistory);
        Assert.Empty(result.Value.Alerts);
    }

    [Fact]
    public async Task GetAsync_ReturnsCriticalItemsFromLatestSnapshot()
    {
        var latest = new MedicalHistoryRecord
        {
            RecordId = 12,
            PatientId = 5,
            VisitId = 71,
            CreatedAt = new DateTime(2026, 10, 2, 9, 30, 0, DateTimeKind.Utc),
            Items =
            [
                new MedicalHistoryItem { ItemId = 22, Type = "Allergy", Name = "Latex", Detail = "Dị ứng tiếp xúc", IsCritical = true },
                new MedicalHistoryItem { ItemId = 23, Type = "Condition", Name = "Viêm mũi", IsCritical = false },
            ],
        };
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5 });
        _medicalHistoryRepository.GetLatestByPatientIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(latest);

        var result = await CreateService().GetAsync(5);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.HasHistory);
        var alert = Assert.Single(result.Value.Alerts);
        Assert.Equal("Allergy", alert.Type);
        Assert.Equal("Latex", alert.Name);
        Assert.Equal("Dị ứng tiếp xúc", alert.Detail);
        Assert.Equal(latest.CreatedAt, alert.RecordedAt);
        Assert.Equal(latest.VisitId, alert.VisitId);
    }

    [Fact]
    public async Task GetAsync_DoesNotReturnCriticalItemsRemovedFromLatestSnapshot()
    {
        var older = new MedicalHistoryRecord
        {
            RecordId = 10,
            PatientId = 5,
            VisitId = 69,
            CreatedAt = new DateTime(2026, 9, 1, 9, 30, 0, DateTimeKind.Utc),
            Items = [new MedicalHistoryItem { ItemId = 18, Type = "Allergy", Name = "Latex", IsCritical = true }],
        };
        var latest = new MedicalHistoryRecord
        {
            RecordId = 12,
            PatientId = 5,
            VisitId = 71,
            CreatedAt = new DateTime(2026, 10, 2, 9, 30, 0, DateTimeKind.Utc),
            Items = [new MedicalHistoryItem { ItemId = 23, Type = "Condition", Name = "Viêm mũi", IsCritical = false }],
        };
        _patientRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Patient { PatientId = 5 });
        _medicalHistoryRepository.GetLatestByPatientIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<MedicalHistoryRecord?>(new[] { older, latest }
                .OrderByDescending(record => record.CreatedAt)
                .ThenByDescending(record => record.RecordId)
                .First()));

        var result = await CreateService().GetAsync(5);

        Assert.True(older.Items.Single().IsCritical);
        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.HasHistory);
        Assert.Empty(result.Value.Alerts);
        await _medicalHistoryRepository.Received(1).GetLatestByPatientIdAsync(
            5, Arg.Any<CancellationToken>());
        await _medicalHistoryRepository.DidNotReceive().GetPatientHistoryAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
