using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dental.Infrastructure.Data;

public sealed class DentalDbContextFactory : IDesignTimeDbContextFactory<DentalDbContext>
{
    public DentalDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DentalDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=dental_clinic;Username=postgres;Password=postgres");
        return new DentalDbContext(optionsBuilder.Options);
    }
}
