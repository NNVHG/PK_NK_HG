using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class VitalSignRecordConfiguration : IEntityTypeConfiguration<VitalSignRecord>
{
    public void Configure(EntityTypeBuilder<VitalSignRecord> builder)
    {
        builder.HasKey(record => record.VitalSignRecordId);

        builder.Property(record => record.Note).HasMaxLength(2000);
        builder.Property(record => record.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();
        builder.Property(record => record.UpdatedAt).HasColumnType("timestamptz");

        builder.HasOne(record => record.Patient)
            .WithMany()
            .HasForeignKey(record => record.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(record => record.Visit)
            .WithMany()
            .HasForeignKey(record => record.VisitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(record => record.RecordedByUser)
            .WithMany()
            .HasForeignKey(record => record.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(record => new { record.PatientId, record.CreatedAt })
            .HasDatabaseName("IX_VitalSignRecords_PatientId_CreatedAt");
        builder.HasIndex(record => record.VisitId)
            .HasDatabaseName("IX_VitalSignRecords_VisitId");
        builder.HasIndex(record => record.RecordedByUserId)
            .HasDatabaseName("IX_VitalSignRecords_RecordedByUserId");
    }
}
