using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> b)
    {
        b.ToTable("InventoryTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.ItemDomain).HasMaxLength(20).IsRequired();
        b.Property(x => x.TransactionType).HasMaxLength(20).IsRequired();
        b.Property(x => x.PayloadHash).HasMaxLength(64);
        b.Property(x => x.SupplierName).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.ConfirmedAt).HasColumnType("timestamptz").IsRequired();

        b.HasIndex(x => new { x.ItemDomain, x.TransactionType, x.RequestId })
            .IsUnique()
            .HasFilter("\"RequestId\" IS NOT NULL");
        b.HasIndex(x => new { x.ItemDomain, x.TransactionType, x.Id });
        b.HasIndex(x => x.VisitId);
        b.HasIndex(x => x.PrescriptionId);

        b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Prescription>().WithMany().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne(x => x.Transaction).HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryTransactionLineConfiguration : IEntityTypeConfiguration<InventoryTransactionLine>
{
    public void Configure(EntityTypeBuilder<InventoryTransactionLine> b)
    {
        b.ToTable("InventoryTransactionLines", t => t.HasCheckConstraint("CK_InventoryTransactionLine_Quantity", "\"Quantity\" > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.ItemCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(50).IsRequired();

        b.HasIndex(x => new { x.TransactionId, x.ItemId }).IsUnique();
        b.HasIndex(x => x.PrescriptionItemId)
            .IsUnique()
            .HasFilter("\"PrescriptionItemId\" IS NOT NULL");
        b.HasIndex(x => x.ItemId);

        b.HasOne<PrescriptionItem>().WithMany().HasForeignKey(x => x.PrescriptionItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
