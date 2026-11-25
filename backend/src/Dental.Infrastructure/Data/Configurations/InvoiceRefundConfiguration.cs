using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class InvoiceRefundConfiguration : IEntityTypeConfiguration<InvoiceRefund>
{
    public void Configure(EntityTypeBuilder<InvoiceRefund> b)
    {
        b.ToTable("InvoiceRefunds", t =>
        {
            t.HasCheckConstraint("CK_InvoiceRefunds_Amounts",
                "\"RefundAmount\" >= 0 AND \"DebtReduction\" >= 0 AND \"TotalAfter\" >= \"DiscountAfter\" AND \"PaidAfter\" >= 0 AND \"PaidAfter\" <= \"TotalAfter\" - \"DiscountAfter\"");
            t.HasCheckConstraint("CK_InvoiceRefunds_Status",
                "\"Status\" IN ('Pending','Approved','Rejected','Stale')");
            t.HasCheckConstraint("CK_InvoiceRefunds_Payment",
                "(\"Status\" <> 'Approved' AND \"PaymentTransactionId\" IS NULL AND \"RefundAmount\" = 0) OR (\"Status\" = 'Approved' AND ((\"RefundAmount\" = 0 AND \"PaymentTransactionId\" IS NULL) OR (\"RefundAmount\" > 0 AND \"PaymentTransactionId\" IS NOT NULL)))");
        });
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => new { x.InvoiceId, x.Revision })
            .IsUnique()
            .HasFilter("\"Status\" = 'Approved'");
        b.HasIndex(x => x.PaymentTransactionId)
            .IsUnique()
            .HasFilter("\"PaymentTransactionId\" IS NOT NULL");
        b.HasIndex(x => new { x.Status, x.CreatedAt });

        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Snapshot).HasMaxLength(64).HasDefaultValue("").IsRequired();
        b.Property(x => x.Status).HasMaxLength(16).HasDefaultValue(InvoiceRefundStatuses.Approved).IsRequired();
        b.Property(x => x.DecisionNotes).HasMaxLength(500);
        b.Property(x => x.ReasonCode).HasMaxLength(40).IsRequired();
        b.Property(x => x.ReasonNotes).HasMaxLength(500).IsRequired();
        b.Property(x => x.PaymentMethod).HasMaxLength(20).IsRequired();
        b.Property(x => x.ItemsJson).HasColumnType("jsonb").HasDefaultValue("[]").IsRequired();
        b.Ignore(x => x.Items);

        foreach (var name in new[] { "RefundAmount", "DebtReduction", "TotalBefore", "DiscountBefore", "PaidBefore", "TotalAfter", "DiscountAfter", "PaidAfter" })
            b.Property<decimal>(name).HasPrecision(18, 0);

        b.HasOne(x => x.Invoice).WithMany(x => x.Refunds).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Reviewer).WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PaymentTransaction).WithMany().HasForeignKey(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
