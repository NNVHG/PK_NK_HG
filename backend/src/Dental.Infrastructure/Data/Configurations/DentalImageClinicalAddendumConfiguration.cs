using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dental.Infrastructure.Data.Configurations;

public sealed class DentalImageClinicalAddendumConfiguration : IEntityTypeConfiguration<DentalImageClinicalAddendum>
{
    public void Configure(EntityTypeBuilder<DentalImageClinicalAddendum> b)
    {
        b.ToTable("DentalImageClinicalAddenda"); b.HasKey(x => x.Id);
        b.Property(x => x.Note).HasMaxLength(1000).IsRequired();
        b.Property(x => x.AuthorName).HasMaxLength(100).IsRequired();
        b.Property(x => x.AuthorRole).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => new { x.DentalImageId, x.Id });
        b.HasOne<DentalImage>().WithMany().HasForeignKey(x => x.DentalImageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
