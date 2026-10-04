using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class DentalServiceConfiguration : IEntityTypeConfiguration<DentalService>
{
    public void Configure(EntityTypeBuilder<DentalService> builder)
    {
        builder.HasKey(service => service.DentalServiceId);

        // [CẦN XÁC NHẬN] Độ dài tối đa chưa được quy định trong tài liệu chức năng.
        builder.Property(service => service.Code).HasMaxLength(50).IsRequired();
        builder.Property(service => service.Name).HasMaxLength(200).IsRequired();
        builder.Property(service => service.Description).HasMaxLength(1000);
        builder.Property(service => service.DurationMinutes).IsRequired();
        builder.Property(service => service.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(service => service.CreatedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(service => service.UpdatedAt).HasColumnType("timestamptz");

        builder.HasIndex(service => service.Code)
            .IsUnique()
            .HasDatabaseName("IX_DentalServices_Code");
    }
}
