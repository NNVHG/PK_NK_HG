using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dental.Infrastructure.Data.Configurations;
public sealed class DoctorSignatureConfiguration : IEntityTypeConfiguration<DoctorSignature>
{
    public void Configure(EntityTypeBuilder<DoctorSignature> b)
    {
        b.ToTable("DoctorSignatures"); b.HasKey(x => x.Id);
        b.Property(x => x.ImagePng).HasColumnType("bytea").IsRequired();
        b.HasIndex(x => new { x.DoctorId, x.Version }).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
