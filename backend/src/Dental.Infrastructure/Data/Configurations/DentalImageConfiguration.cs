using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dental.Infrastructure.Data.Configurations;

public sealed class DentalImageConfiguration : IEntityTypeConfiguration<DentalImage>
{
    public void Configure(EntityTypeBuilder<DentalImage> b)
    {
        b.ToTable("DentalImages"); b.HasKey(x => x.Id);
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.ClinicalNotes).HasColumnType("text").HasMaxLength(1000);
        b.Property(x => x.DoctorDiagnosis).HasMaxLength(255);
        b.Property(x => x.ClinicalUpdatedAt).HasColumnType("timestamptz");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ClinicalUpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.OriginalFileName).HasMaxLength(180).IsRequired();
        b.Property(x => x.StoragePath).HasMaxLength(180).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(30).IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.StoragePath).IsUnique();
        b.HasIndex(x => new { x.PatientId, x.VisitId, x.CreatedAt });
        b.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
