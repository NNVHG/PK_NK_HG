using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");
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

        builder.Property(a => a.Notes)
            .HasMaxLength(500);

        builder.Property(a => a.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.HasOne(a => a.Patient)
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Dentist)
            .WithMany()
            .HasForeignKey(a => a.DentistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CreatedByUser)
            .WithMany()
            .HasForeignKey(a => a.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.AppointmentDate, a.SlotTime })
            .HasDatabaseName("IX_Appointments_AppointmentDate_SlotTime");

        builder.HasIndex(a => new { a.PatientId, a.AppointmentDate })
            .HasDatabaseName("IX_Appointments_PatientId_AppointmentDate");

        builder.HasIndex(a => a.DentistId)
            .HasDatabaseName("IX_Appointments_DentistId");

        builder.HasIndex(a => a.CreatedByUserId)
            .HasDatabaseName("IX_Appointments_CreatedByUserId");
    }
}
