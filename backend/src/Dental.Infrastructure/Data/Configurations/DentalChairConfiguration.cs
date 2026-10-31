using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class DentalChairConfiguration : IEntityTypeConfiguration<DentalChair>
{
    public void Configure(EntityTypeBuilder<DentalChair> b)
    {
        b.ToTable("DentalChairs"); b.HasKey(x => x.Id);
        b.Property(x => x.ChairNumber).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.ChairNumber).IsUnique().HasDatabaseName("IX_DentalChairs_ChairNumber");
        b.Property(x => x.RoomName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.RoomId).HasDatabaseName("IX_DentalChairs_RoomId");
        b.HasOne<ClinicRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
