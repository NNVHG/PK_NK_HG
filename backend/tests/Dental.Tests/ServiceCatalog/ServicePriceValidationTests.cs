using Dental.Application.Features.ServiceCatalog.DTOs;
using Dental.Application.Features.ServiceCatalog.Validators;
using Dental.Tests.Common;
using Xunit;

namespace Dental.Tests.ServiceCatalog;

public sealed class ServicePriceValidationTests
{
    private static TimeProvider Clock => new FixedTimeProvider(new DateTimeOffset(2026, 10, 15, 17, 1, 0, TimeSpan.Zero));
    private static DateTimeOffset Today => new(2026, 10, 16, 0, 0, 0, TimeSpan.FromHours(7));
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1.5, false)]
    [InlineData(100001, true)]
    public void InitialAndSubsequentPrices_RequireWholeNonnegativeVnd(decimal amount, bool valid)
    {
        Assert.Equal(valid, new CreateDentalServiceRequestValidator(Clock).Validate(new CreateDentalServiceRequest("SYN", "Synthetic", null, 30, amount, Today)).IsValid);
        Assert.Equal(valid, new CreateServicePriceRequestValidator(Clock).Validate(new CreateServicePriceRequest(amount, Today)).IsValid);
    }
    [Fact]
    public void Prices_RespectNumeric18Maximum()
    {
        var validator = new CreateServicePriceRequestValidator(Clock);
        Assert.True(validator.Validate(new CreateServicePriceRequest(999999999999999999m, Today)).IsValid);
        Assert.False(validator.Validate(new CreateServicePriceRequest(1000000000000000000m, Today)).IsValid);
    }
    [Fact]
    public void EffectiveDay_UsesVietnamBoundaryEvenWhenUtcIsStillPreviousDay()
    {
        foreach (var instant in new[] { Today, Today.ToUniversalTime(), Today.AddDays(1) })
        {
            Assert.True(new CreateDentalServiceRequestValidator(Clock).Validate(new CreateDentalServiceRequest("SYN", "Synthetic", null, 30, 100, instant)).IsValid);
            Assert.True(new CreateServicePriceRequestValidator(Clock).Validate(new CreateServicePriceRequest(100, instant)).IsValid);
        }
        Assert.False(new CreateDentalServiceRequestValidator(Clock).Validate(new CreateDentalServiceRequest("SYN", "Synthetic", null, 30, 100, Today.AddTicks(-1))).IsValid);
        Assert.False(new CreateServicePriceRequestValidator(Clock).Validate(new CreateServicePriceRequest(100, Today.AddTicks(-1))).IsValid);
        Assert.False(new CreateServicePriceRequestValidator(Clock).Validate(new CreateServicePriceRequest(100, default)).IsValid);
    }
}
