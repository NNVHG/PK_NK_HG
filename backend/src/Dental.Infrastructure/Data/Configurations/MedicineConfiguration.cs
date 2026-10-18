using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dental.Infrastructure.Data.Configurations;
public sealed class MedicineConfiguration : IEntityTypeConfiguration<Medicine>
{
    public void Configure(EntityTypeBuilder<Medicine> b)
    {
        b.ToTable("Medicines"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.Property(x => x.ActiveIngredient).HasMaxLength(200);
        b.Property(x => x.Strength).HasMaxLength(50); b.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        b.Property(x => x.UnitPrice).HasPrecision(18, 0); b.Property(x => x.DefaultUsageInstruction).HasMaxLength(500);
        b.Property(x => x.SearchText).HasMaxLength(600).IsRequired(); b.Property(x => x.Version).IsConcurrencyToken();
    }
}
