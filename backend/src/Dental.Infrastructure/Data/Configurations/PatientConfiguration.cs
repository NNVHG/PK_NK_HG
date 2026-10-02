using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.HasKey(patient => patient.PatientId);

        builder.Property(patient => patient.PatientNumber)
            .IsRequired()
            .UseSequence("patient_number_seq");

        builder.Ignore(patient => patient.PatientCode);

        builder.Property(patient => patient.FullName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(patient => patient.DateOfBirth)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(patient => patient.Gender)
            .HasMaxLength(10);

        builder.Property(patient => patient.Phone)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(patient => patient.Email)
            .HasMaxLength(100);

        builder.Property(patient => patient.Address)
            .HasMaxLength(500);

        builder.Property(patient => patient.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(patient => patient.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(patient => patient.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.HasIndex(patient => patient.PatientNumber)
            .IsUnique()
            .HasDatabaseName("IX_Patients_PatientNumber");

        builder.HasIndex(patient => patient.Phone)
            .HasDatabaseName("IX_Patients_Phone");

        builder.HasIndex(patient => patient.FullName)
            .HasDatabaseName("IX_Patients_FullName");

        builder.HasOne(patient => patient.User)
            .WithOne()
            .HasForeignKey<Patient>(patient => patient.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(patient => patient.UserId)
            .IsUnique()
            .HasDatabaseName("IX_Patients_UserId");
    }
}
