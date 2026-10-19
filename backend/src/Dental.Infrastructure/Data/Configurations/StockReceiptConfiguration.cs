using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class StockReceiptConfiguration : IEntityTypeConfiguration<StockReceipt>
{
    public void Configure(EntityTypeBuilder<StockReceipt> b)
    {
        b.ToTable("StockReceipts");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.RequestId).IsUnique();
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.SupplierName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne(x => x.Receipt).HasForeignKey(x => x.StockReceiptId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StockReceiptLineConfiguration : IEntityTypeConfiguration<StockReceiptLine>
{
    public void Configure(EntityTypeBuilder<StockReceiptLine> b)
    {
        b.ToTable("StockReceiptLines", t => t.HasCheckConstraint("CK_StockReceiptLine_Quantity", "\"Quantity\" > 0"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.StockReceiptId, x.MedicalSupplyId }).IsUnique();
        b.Property(x => x.SupplyCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.SupplyName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(50).IsRequired();
        b.HasOne<MedicalSupply>().WithMany().HasForeignKey(x => x.MedicalSupplyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> b)
    {
        b.ToTable("StockBalances", t => t.HasCheckConstraint("CK_StockBalance_OnHand", "\"OnHand\" >= 0"));
        b.HasKey(x => x.MedicalSupplyId);
        b.Property(x => x.MedicalSupplyId).ValueGeneratedNever();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne<MedicalSupply>().WithMany().HasForeignKey(x => x.MedicalSupplyId).OnDelete(DeleteBehavior.Restrict);
    }
}
