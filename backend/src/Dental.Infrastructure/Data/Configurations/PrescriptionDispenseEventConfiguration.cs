using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dental.Infrastructure.Data.Configurations;
public sealed class PrescriptionDispenseEventConfiguration : IEntityTypeConfiguration<PrescriptionDispenseEvent>
{
    public void Configure(EntityTypeBuilder<PrescriptionDispenseEvent> b)
    {
        b.ToTable("PrescriptionDispenseEvents"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.PrescriptionId, x.PrescriptionVersion }).IsUnique();
        b.Property(x => x.Action).HasMaxLength(30).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.ReceiverName).HasMaxLength(100);
        b.Property(x => x.ReceiverPhone).HasMaxLength(20);
        b.HasOne<Prescription>().WithMany().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PerformedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
