using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments", t =>
        {
            t.HasCheckConstraint("CK_Appointments_Duration", "\"SlotDurationMinutes\" BETWEEN 1 AND 1440");
            t.HasCheckConstraint("CK_Appointments_Version", "\"Version\" > 0");
        });
        builder.Property(x => x.SlotDurationMinutes).HasDefaultValue(30).IsRequired();
        builder.HasKey(a => a.AppointmentId);

        builder.Property(a => a.AppointmentDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(a => a.SlotTime)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(a => a.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Source)
            .HasMaxLength(20)
            .HasDefaultValue("Counter")
            .IsRequired();

        builder.Property(a => a.Notes)
            .HasMaxLength(500);
        builder.Property(a => a.RescheduleCount).HasDefaultValue(0);
        builder.Property(a => a.RescheduleReason).HasMaxLength(500);
        builder.Property(a => a.IsEmergency).HasDefaultValue(false).IsRequired();
        builder.Property(a => a.EmergencyReason).HasMaxLength(255);

        builder.Property(a => a.IntentDigest).HasMaxLength(64);
        builder.Property(a => a.GuestFullName).HasMaxLength(100);
        builder.Property(a => a.GuestPhone).HasMaxLength(20);
        builder.Property(a => a.GuestDateOfBirth).HasColumnType("date");
        builder.Property(a => a.GuestGender).HasMaxLength(10);
        builder.Property(a => a.GuestEmail).HasMaxLength(100);
        builder.Property(a => a.Version).HasDefaultValue(1).IsConcurrencyToken();
        builder.Property(a => a.RejectionReason).HasMaxLength(500);

        builder.Property(a => a.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.HasOne(a => a.Patient)
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CreatedByUser)
            .WithMany()
            .HasForeignKey(a => a.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.ClientRequestId)
            .IsUnique()
            .HasFilter("\"ClientRequestId\" IS NOT NULL");

        builder.HasIndex(a => new { a.Status, a.AppointmentDate, a.SlotTime });

        builder.HasIndex(a => new { a.AppointmentDate, a.SlotTime })
            .HasDatabaseName("IX_Appointments_AppointmentDate_SlotTime");

        builder.HasIndex(a => new { a.PatientId, a.AppointmentDate })
            .HasDatabaseName("IX_Appointments_PatientId_AppointmentDate");

        builder.HasIndex(a => a.CreatedByUserId)
            .HasDatabaseName("IX_Appointments_CreatedByUserId");
    }
}
