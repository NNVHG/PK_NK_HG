using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class MedicineStockBalanceConfiguration : IEntityTypeConfiguration<MedicineStockBalance>
{
    public void Configure(EntityTypeBuilder<MedicineStockBalance> b)
    {
        b.ToTable("MedicineStockBalances", t => t.HasCheckConstraint("CK_MedicineStockBalance_Nonnegative", "\"OnHand\" >= 0"));
        b.HasKey(x => x.MedicineId); b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne<Medicine>().WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class MedicineStockReceiptConfiguration : IEntityTypeConfiguration<MedicineStockReceipt>
{
    public void Configure(EntityTypeBuilder<MedicineStockReceipt> b)
    {
        b.ToTable("MedicineStockReceipts", t => t.HasCheckConstraint("CK_MedicineStockReceipt_Quantity", "\"Quantity\" > 0")); b.HasKey(x => x.Id);
        b.HasIndex(x => x.RequestId).IsUnique(); b.HasIndex(x => new { x.MedicineId, x.Id });
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired(); b.Property(x => x.MedicineCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.MedicineName).HasMaxLength(200).IsRequired(); b.Property(x => x.Unit).HasMaxLength(50).IsRequired();
        b.Property(x => x.SupplierName).HasMaxLength(200).IsRequired(); b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne<Medicine>().WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class MedicineStockIssueConfiguration : IEntityTypeConfiguration<MedicineStockIssue>
{
    public void Configure(EntityTypeBuilder<MedicineStockIssue> b)
    {
        b.ToTable("MedicineStockIssues", t => t.HasCheckConstraint("CK_MedicineStockIssue_Quantity", "\"Quantity\" > 0")); b.HasKey(x => x.Id);
        b.HasIndex(x => x.PrescriptionItemId).IsUnique(); b.HasIndex(x => new { x.PrescriptionId, x.MedicineId }).IsUnique();
        b.Property(x => x.MedicineCode).HasMaxLength(50).IsRequired(); b.Property(x => x.MedicineName).HasMaxLength(200).IsRequired(); b.Property(x => x.Unit).HasMaxLength(50).IsRequired();
        b.HasOne<Medicine>().WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Prescription>().WithMany().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PrescriptionItem>().WithMany().HasForeignKey(x => x.PrescriptionItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DispenseEvent).WithMany().HasForeignKey(x => x.DispenseEventId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
