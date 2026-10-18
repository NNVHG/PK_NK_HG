using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dental.Infrastructure.Data.Configurations;
public sealed class InvoiceRefundConfiguration : IEntityTypeConfiguration<InvoiceRefund>
{
    public void Configure(EntityTypeBuilder<InvoiceRefund> b)
    {
        b.ToTable("InvoiceRefunds", t => {
            t.HasCheckConstraint("CK_InvoiceRefunds_Amounts", "\"RefundAmount\" >= 0 AND \"DebtReduction\" >= 0 AND \"TotalAfter\" >= \"DiscountAfter\" AND \"PaidAfter\" >= 0 AND \"PaidAfter\" <= \"TotalAfter\" - \"DiscountAfter\"");
            t.HasCheckConstraint("CK_InvoiceRefunds_Payment", "(\"RefundAmount\" = 0 AND \"PaymentTransactionId\" IS NULL) OR (\"RefundAmount\" > 0 AND \"PaymentTransactionId\" IS NOT NULL)");
        });
        b.HasKey(x => x.Id); b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => new { x.InvoiceId, x.Revision }).IsUnique();
        b.HasIndex(x => x.PaymentTransactionId).IsUnique();
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.ReasonCode).HasMaxLength(40).IsRequired();
        b.Property(x => x.ReasonNotes).HasMaxLength(500).IsRequired();
        b.Property(x => x.PaymentMethod).HasMaxLength(20).IsRequired();
        foreach (var name in new[] { "RefundAmount", "DebtReduction", "TotalBefore", "DiscountBefore", "PaidBefore", "TotalAfter", "DiscountAfter", "PaidAfter" })
            b.Property<decimal>(name).HasPrecision(18, 0);
        b.HasOne(x => x.Invoice).WithMany(x => x.Refunds).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PaymentTransaction).WithMany().HasForeignKey(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class InvoiceRefundItemConfiguration : IEntityTypeConfiguration<InvoiceRefundItem>
{
    public void Configure(EntityTypeBuilder<InvoiceRefundItem> b)
    {
        b.ToTable("InvoiceRefundItems",t=>t.HasCheckConstraint("CK_InvoiceRefundItems_Quantity","\"Quantity\" > 0")); b.HasKey(x=>x.Id);
        b.HasIndex(x=>new{x.InvoiceRefundId,x.InvoiceItemId}).IsUnique();
        b.HasOne(x=>x.InvoiceRefund).WithMany(x=>x.Items).HasForeignKey(x=>x.InvoiceRefundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x=>x.InvoiceItem).WithMany().HasForeignKey(x=>x.InvoiceItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
