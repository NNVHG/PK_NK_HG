using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class QueueStatusHistoryConfiguration : IEntityTypeConfiguration<QueueStatusHistory>
{
    public void Configure(EntityTypeBuilder<QueueStatusHistory> builder)
    {
        builder.ToTable("QueueStatusHistories");
        builder.HasKey(h => h.QueueStatusHistoryId);

        builder.Property(h => h.FromStatus)
            .HasConversion<int?>();

        builder.Property(h => h.ToStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(h => h.ChangedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(h => h.Reason)
            .HasMaxLength(500);

        builder.HasOne(h => h.QueueEntry)
            .WithMany(q => q.StatusHistories)
            .HasForeignKey(h => h.QueueEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.QueueEntryId, h.ChangedAt })
            .HasDatabaseName("IX_QueueStatusHistories_QueueEntryId_ChangedAt");

        builder.HasIndex(h => h.ChangedByUserId)
            .HasDatabaseName("IX_QueueStatusHistories_ChangedByUserId");
    }
}
