using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class DiscountPresetConfiguration : IEntityTypeConfiguration<DiscountPreset>
{
    public void Configure(EntityTypeBuilder<DiscountPreset> b)
    {
        b.ToTable("DiscountPresets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.DiscountType).HasMaxLength(20).IsRequired();
        // Mixed metadata: percentage may have 2 decimal places; fixed VND is validated as an integer.
        b.Property(x => x.Value).HasPrecision(20, 2);
    }
}
