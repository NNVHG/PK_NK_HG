using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;
public sealed class StaffWorkspaceConfiguration : IEntityTypeConfiguration<StaffWorkspace>
{
    public void Configure(EntityTypeBuilder<StaffWorkspace> b)
    {
        b.ToTable("StaffWorkspaces"); b.HasKey(x => x.UserId);
        b.Property(x => x.UserId).ValueGeneratedNever();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.ChairId).IsUnique().HasDatabaseName("IX_StaffWorkspaces_ChairId");
        b.HasIndex(x => x.DentistId).HasDatabaseName("IX_StaffWorkspaces_DentistId");
        b.HasOne(x => x.User).WithOne().HasForeignKey<StaffWorkspace>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DentalChair>().WithMany().HasForeignKey(x => x.ChairId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DentistId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
