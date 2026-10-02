using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class MedicalHistoryRecordConfiguration : IEntityTypeConfiguration<MedicalHistoryRecord>
{
    public void Configure(EntityTypeBuilder<MedicalHistoryRecord> builder)
    {
        builder.HasKey(record => record.RecordId);

        builder.Property(record => record.Note)
            .HasMaxLength(2000);

        builder.Property(record => record.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();
        builder.Property(record => record.UpdatedAt)
            .HasColumnType("timestamptz");

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

        builder.HasMany(record => record.Items)
            .WithOne(item => item.Record)
            .HasForeignKey(item => item.RecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(record => new { record.PatientId, record.CreatedAt })
            .HasDatabaseName("IX_MedicalHistoryRecords_PatientId_CreatedAt");
        builder.HasIndex(record => record.VisitId)
            .HasDatabaseName("IX_MedicalHistoryRecords_VisitId");
        builder.HasIndex(record => record.RecordedByUserId)
            .HasDatabaseName("IX_MedicalHistoryRecords_RecordedByUserId");
    }
}
