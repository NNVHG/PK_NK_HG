using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(r => r.RoleId);

        builder.Property(r => r.RoleCode)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.RoleName)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasMaxLength(200);

        // Index unique cho RoleCode (dùng trong JWT/policy)
        builder.HasIndex(r => r.RoleCode)
            .IsUnique()
            .HasDatabaseName("IX_Roles_RoleCode");

        // Index unique cho RoleName
        builder.HasIndex(r => r.RoleName)
            .IsUnique()
            .HasDatabaseName("IX_Roles_RoleName");
    }
}
