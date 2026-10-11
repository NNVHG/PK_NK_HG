using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class VisitServiceConfiguration : IEntityTypeConfiguration<VisitService>
{
    public void Configure(EntityTypeBuilder<VisitService> builder)
    {
        builder.ToTable("VisitServices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ServiceName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Surface).HasMaxLength(10);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 0);
        builder.HasIndex(x => new { x.VisitId, x.ToothNumber });
        builder.HasOne(x => x.Visit).WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Price).WithMany().HasForeignKey(x => x.ServicePriceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
