using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dental.Infrastructure.Data.Configurations;
public sealed class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> b)
    {
        b.ToTable("Prescriptions"); b.HasKey(x => x.Id);
        b.Property(x => x.PrescriptionCode).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.PrescriptionCode).IsUnique();
        // DL-296: keep cancelled history; enforce one effective prescription at the database boundary.
        b.HasIndex(x => x.VisitId).IsUnique().HasFilter("\"IsCancelled\" = false");
        b.HasIndex(x => new { x.VisitId, x.Id });
        b.Property(x => x.Diagnosis).HasMaxLength(500).IsRequired();
        b.Property(x => x.DoctorAdvice).HasMaxLength(1000); b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.DispenseNote).HasMaxLength(300);
        b.Property(x => x.ReceiverName).HasMaxLength(100); b.Property(x => x.ReceiverPhone).HasMaxLength(20);
        b.HasOne(x => x.Visit).WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DispensedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> b)
    {
        b.ToTable("PrescriptionItems"); b.HasKey(x => x.Id);
        b.HasOne(x => x.Prescription).WithMany(x => x.Items).HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Medicine>().WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PrescriptionId, x.MedicineId }).IsUnique();
        b.Property(x => x.MedicineCode).HasMaxLength(50).IsRequired(); b.Property(x => x.MedicineName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ActiveIngredient).HasMaxLength(200); b.Property(x => x.Strength).HasMaxLength(50);
        b.Property(x => x.Unit).HasMaxLength(20).IsRequired(); b.Property(x => x.UnitPrice).HasPrecision(18, 0); b.Ignore(x => x.TotalPrice);
        b.Property(x => x.UsageInstructions).HasMaxLength(500).IsRequired(); b.Property(x => x.AllergyOverrideReason).HasMaxLength(500);
    }
}
public sealed class PrescriptionNumberCounterConfiguration : IEntityTypeConfiguration<PrescriptionNumberCounter>
{
    public void Configure(EntityTypeBuilder<PrescriptionNumberCounter> b) { b.ToTable("PrescriptionNumberCounters"); b.HasKey(x => x.PrescriptionDate); }
}
