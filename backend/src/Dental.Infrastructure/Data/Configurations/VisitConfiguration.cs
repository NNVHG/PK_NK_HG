using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.HasKey(visit => visit.VisitId);

        builder.Property(visit => visit.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(visit => visit.Diagnosis)
            .HasMaxLength(1000);

        builder.Property(visit => visit.ClinicalNotes)
            .HasMaxLength(2000);

        builder.Property(visit => visit.StartedAt).HasColumnType("timestamptz");
        builder.Property(visit => visit.EndedAt).HasColumnType("timestamptz");
        builder.Property(visit => visit.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();
        builder.Property(visit => visit.UpdatedAt).HasColumnType("timestamptz");

        builder.HasOne(visit => visit.Patient)
            .WithMany()
            .HasForeignKey(visit => visit.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(visit => visit.Dentist)
            .WithMany()
            .HasForeignKey(visit => visit.DentistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(visit => visit.CreatedByUser)
            .WithMany()
            .HasForeignKey(visit => visit.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(visit => new { visit.PatientId, visit.CreatedAt })
            .HasDatabaseName("IX_Visits_PatientId_CreatedAt");
        builder.HasIndex(visit => visit.DentistId)
            .HasDatabaseName("IX_Visits_DentistId");
        builder.HasIndex(visit => visit.CreatedByUserId)
            .HasDatabaseName("IX_Visits_CreatedByUserId");
    }
}
