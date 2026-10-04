using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class ServicePriceConfiguration : IEntityTypeConfiguration<ServicePrice>
{
    public void Configure(EntityTypeBuilder<ServicePrice> builder)
    {
        builder.HasKey(price => price.ServicePriceId);

        // [CẦN XÁC NHẬN] Tạm dùng decimal(18,0) cho VND theo chỉ dẫn; cần xác nhận quy ước tiền tệ của dự án.
        builder.Property(price => price.Amount).HasPrecision(18, 0).IsRequired();
        builder.Property(price => price.EffectiveFrom).HasColumnType("timestamptz").IsRequired();
        builder.Property(price => price.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(price => price.UpdatedAt).HasColumnType("timestamptz");

        builder.HasOne(price => price.Service)
            .WithMany(service => service.Prices)
            .HasForeignKey(price => price.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(price => price.CreatedByUser)
            .WithMany()
            .HasForeignKey(price => price.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(price => new { price.ServiceId, price.EffectiveFrom, price.ServicePriceId })
            .HasDatabaseName("IX_ServicePrices_ServiceId_EffectiveFrom_ServicePriceId");
        builder.HasIndex(price => price.CreatedByUserId)
            .HasDatabaseName("IX_ServicePrices_CreatedByUserId");
    }
}
