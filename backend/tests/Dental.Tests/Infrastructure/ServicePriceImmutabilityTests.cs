using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dental.Tests.Infrastructure;

public sealed class ServicePriceImmutabilityTests
{
    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task SaveChangesAsync_RejectsServicePriceUpdateOrDelete(EntityState state)
    {
        var options = new DbContextOptionsBuilder<DentalDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        await using var context = new DentalDbContext(options);
        var price = new ServicePrice { ServicePriceId = 1, ServiceId = 1, CreatedByUserId = 1 };
        context.ServicePrices.Attach(price);
        context.Entry(price).State = state;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

        Assert.Contains("chỉ thêm mới", exception.Message);
    }
}
