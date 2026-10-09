using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class QueueEntryConfiguration : IEntityTypeConfiguration<QueueEntry>
{
    public void Configure(EntityTypeBuilder<QueueEntry> builder)
    {
        builder.ToTable("QueueEntries");
        builder.HasKey(q => q.QueueEntryId);

        builder.Property(q => q.QueueDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(q => q.QueueNumber)
            .IsRequired();

        builder.Property(q => q.IsPriority)
            .IsRequired();

        builder.Property(q => q.CheckInTime)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(q => q.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(q => q.Notes)
            .HasMaxLength(500);

        builder.Property(q => q.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(q => q.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.HasOne(q => q.Patient)
            .WithMany()
            .HasForeignKey(q => q.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Appointment)
            .WithMany()
            .HasForeignKey(q => q.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Dentist)
            .WithMany()
            .HasForeignKey(q => q.DentistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Visit)
            .WithMany()
            .HasForeignKey(q => q.VisitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.StatusHistories)
            .WithOne(h => h.QueueEntry)
            .HasForeignKey(h => h.QueueEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ràng buộc duy nhất theo ngày để chống trùng số khi check-in đồng thời (DL-046)
        builder.HasIndex(q => new { q.QueueDate, q.QueueNumber })
            .IsUnique()
            .HasDatabaseName("IX_QueueEntries_QueueDate_QueueNumber");

        builder.HasIndex(q => new { q.PatientId, q.QueueDate })
            .HasDatabaseName("IX_QueueEntries_PatientId_QueueDate");

        builder.HasIndex(q => new { q.QueueDate, q.Status })
            .HasDatabaseName("IX_QueueEntries_QueueDate_Status");

        builder.HasIndex(q => q.AppointmentId)
            .HasDatabaseName("IX_QueueEntries_AppointmentId");

        builder.HasIndex(q => q.DentistId)
            .HasDatabaseName("IX_QueueEntries_DentistId");

        builder.HasIndex(q => q.VisitId)
            .HasDatabaseName("IX_QueueEntries_VisitId");
    }
}
