using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Validators;
using Xunit;

namespace Dental.Tests.Common;

public sealed class VietnamClockTests
{
    [Theory]
    [InlineData("2026-04-10T16:59:59+00:00", "2026-04-10")]
    [InlineData("2026-04-10T17:00:01+00:00", "2026-04-11")]
    public void Today_UsesVietnamDateAcrossUtcMidnightBoundary(string utcNow, string expectedDate)
    {
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse(utcNow));
        var clock = new VietnamClock(timeProvider);

        Assert.Equal(DateOnly.Parse(expectedDate), clock.Today);
    }

    [Fact]
    public async Task PatientDateValidator_UsesVietnamDayAtUtcMidnightBoundary()
    {
        var dob = new DateOnly(2026, 4, 11);
        var request = new CreatePatientRequest("Nguyễn An", dob, "Female", "0912345678", null);
        var beforeVietnamMidnight = new CreatePatientRequestValidator(
            new VietnamClock(new FixedTimeProvider(DateTimeOffset.Parse("2026-04-10T16:59:59+00:00"))));
        var afterVietnamMidnight = new CreatePatientRequestValidator(
            new VietnamClock(new FixedTimeProvider(DateTimeOffset.Parse("2026-04-10T17:00:01+00:00"))));

        Assert.Contains((await beforeVietnamMidnight.ValidateAsync(request)).Errors,
            error => error.PropertyName == nameof(CreatePatientRequest.DateOfBirth));
        Assert.DoesNotContain((await afterVietnamMidnight.ValidateAsync(request)).Errors,
            error => error.PropertyName == nameof(CreatePatientRequest.DateOfBirth));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
