using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class ClinicRoomConfiguration : IEntityTypeConfiguration<ClinicRoom>
{
    public void Configure(EntityTypeBuilder<ClinicRoom> b)
    {
        b.ToTable("ClinicRooms");
        b.HasKey(x => x.Id);
        b.Property(x => x.RoomCode).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.RoomCode).IsUnique().HasDatabaseName("IX_ClinicRooms_RoomCode");
        b.Property(x => x.RoomName).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.RoomName).IsUnique().HasDatabaseName("IX_ClinicRooms_RoomName");
        b.Property(x => x.NormalizedRoomName).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.NormalizedRoomName).IsUnique().HasDatabaseName("IX_ClinicRooms_NormalizedRoomName");
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.LegacyChairId).IsUnique()
            .HasFilter("\"LegacyChairId\" IS NOT NULL")
            .HasDatabaseName("IX_ClinicRooms_LegacyChairId");
        b.HasOne<DentalChair>().WithMany().HasForeignKey(x => x.LegacyChairId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
