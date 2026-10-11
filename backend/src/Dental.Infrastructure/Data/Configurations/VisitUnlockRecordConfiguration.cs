using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class VisitUnlockRecordConfiguration : IEntityTypeConfiguration<VisitUnlockRecord>
{
    public void Configure(EntityTypeBuilder<VisitUnlockRecord> b)
    {
        b.ToTable("VisitUnlockRecords"); b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.HasOne(x => x.Visit).WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CancelledInvoice).WithMany().HasForeignKey(x => x.CancelledInvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
