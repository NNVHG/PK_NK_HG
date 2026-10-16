using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class PostTreatmentNoteConfiguration : IEntityTypeConfiguration<PostTreatmentNote>
{
    public void Configure(EntityTypeBuilder<PostTreatmentNote> b)
    {
        b.ToTable("PostTreatmentNotes");
        b.HasKey(x => x.Id);

        b.Property(x => x.Note)
            .HasMaxLength(1000)
            .IsRequired();

        b.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        b.Property(x => x.RequestId);

        b.HasOne(x => x.Visit)
            .WithMany()
            .HasForeignKey(x => x.VisitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.VisitId, x.CreatedAt })
            .HasDatabaseName("IX_PostTreatmentNotes_VisitId_CreatedAt");

        b.HasIndex(x => x.RequestId)
            .IsUnique()
            .HasFilter("\"RequestId\" IS NOT NULL")
            .HasDatabaseName("IX_PostTreatmentNotes_RequestId");
    }
}
