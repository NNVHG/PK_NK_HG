using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class PatientGuardianConfiguration : IEntityTypeConfiguration<PatientGuardian>
{
    public void Configure(EntityTypeBuilder<PatientGuardian> builder)
    {
        builder.HasKey(g => g.PatientGuardianId);

        builder.Property(g => g.Relationship)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(g => g.Notes)
            .HasMaxLength(500);

        builder.Property(g => g.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(g => g.AssignedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(g => g.RevokedAt)
            .HasColumnType("timestamptz");

        builder.Property(g => g.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(g => g.UpdatedAt)
            .HasColumnType("timestamptz");

        // Foreign keys
        builder.HasOne(g => g.GuardianPatient)
            .WithMany()
            .HasForeignKey(g => g.GuardianPatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.WardPatient)
            .WithMany()
            .HasForeignKey(g => g.WardPatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.AssignedByUser)
            .WithMany()
            .HasForeignKey(g => g.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.RevokedByUser)
            .WithMany()
            .HasForeignKey(g => g.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // DL-353, DL-358: 1 trẻ chỉ có tối đa 1 giám hộ hiệu lực tại một thời điểm
        builder.HasIndex(g => g.WardPatientId)
            .IsUnique()
            .HasFilter("\"IsActive\" = true")
            .HasDatabaseName("IX_PatientGuardians_WardPatientId_Active");

        // 1 giám hộ có thể quản lý nhiều trẻ
        builder.HasIndex(g => g.GuardianPatientId)
            .HasDatabaseName("IX_PatientGuardians_GuardianPatientId");
    }
}
