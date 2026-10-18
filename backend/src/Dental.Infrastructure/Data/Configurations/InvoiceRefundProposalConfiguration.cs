using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class InvoiceRefundProposalConfiguration : IEntityTypeConfiguration<InvoiceRefundProposal>
{
    public void Configure(EntityTypeBuilder<InvoiceRefundProposal> b)
    {
        b.ToTable("InvoiceRefundProposals", t => t.HasCheckConstraint("CK_InvoiceRefundProposals_Status", "\"Status\" IN ('Pending','Approved','Rejected','Stale')"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.Property(x => x.Snapshot).HasMaxLength(64).IsRequired();
        b.Property(x => x.ReasonCode).HasMaxLength(40).IsRequired();
        b.Property(x => x.ReasonNotes).HasMaxLength(500).IsRequired();
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.DecisionNotes).HasMaxLength(500);
        b.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Reviewer).WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InvoiceRefundProposalItemConfiguration : IEntityTypeConfiguration<InvoiceRefundProposalItem>
{
    public void Configure(EntityTypeBuilder<InvoiceRefundProposalItem> b)
    {
        b.ToTable("InvoiceRefundProposalItems", t => t.HasCheckConstraint("CK_InvoiceRefundProposalItems_Quantity", "\"Quantity\" > 0"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ProposalId, x.InvoiceItemId }).IsUnique();
        b.HasOne(x => x.Proposal).WithMany(x => x.Items).HasForeignKey(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.InvoiceItem).WithMany().HasForeignKey(x => x.InvoiceItemId).OnDelete(DeleteBehavior.Restrict);
    }
}