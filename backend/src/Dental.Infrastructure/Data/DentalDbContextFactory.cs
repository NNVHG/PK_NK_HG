using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dental.Infrastructure.Data;

public sealed class DentalDbContextFactory : IDesignTimeDbContextFactory<DentalDbContext>
{
    public DentalDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DentalDbContext>();
        // Lệnh kiểm tra mô hình vẫn chạy được khi chưa chỉ định kết nối CSDL.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        optionsBuilder.UseNpgsql(string.IsNullOrWhiteSpace(connectionString)
            ? "Host=127.0.0.1;Port=1;Database=ef_design_time;Username=ef_design_time;Timeout=2"
            : connectionString);
        return new DentalDbContext(optionsBuilder.Options);
    }
}
