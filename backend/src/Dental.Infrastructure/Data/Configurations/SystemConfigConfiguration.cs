using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> b)
    {
        b.ToTable("SystemConfigs"); b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasColumnName("ConfigKey").HasMaxLength(100).IsRequired();
        b.Property(x => x.Value).HasColumnName("ConfigValue").HasMaxLength(1000).IsRequired();
        b.HasIndex(x => x.Key).IsUnique();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.DataType).HasMaxLength(50).HasDefaultValue("String").IsRequired();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
