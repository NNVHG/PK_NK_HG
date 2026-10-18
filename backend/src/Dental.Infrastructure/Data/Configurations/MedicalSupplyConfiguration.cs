using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class MedicalSupplyConfiguration : IEntityTypeConfiguration<MedicalSupply>
{
    public void Configure(EntityTypeBuilder<MedicalSupply> b)
    {
        b.ToTable("MedicalSupplies", t => t.HasCheckConstraint("CK_MedicalSupplies_MinimumStockThreshold", "\"MinimumStockThreshold\" >= 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("IX_MedicalSupplies_Code");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(50).IsRequired();
        b.Property(x => x.MinimumStockThreshold).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.SearchText).HasMaxLength(600).IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
