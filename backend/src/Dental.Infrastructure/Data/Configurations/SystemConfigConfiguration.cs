using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> b)
    {
        b.ToTable("SystemConfigs"); b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(100); b.Property(x => x.Value).HasMaxLength(1000).IsRequired();
    }
}
