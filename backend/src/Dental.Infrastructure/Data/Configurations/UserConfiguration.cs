using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.UserId);
        builder.Property(u => u.TokenVersion).HasDefaultValue(0).IsConcurrencyToken();
        builder.Property(u => u.RequiresPhoneActivation).HasDefaultValue(false).IsConcurrencyToken();
        builder.Property(u => u.PhoneVerifiedAtUtc).HasColumnType("timestamptz");

        builder.Property(u => u.Phone)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.AcademicTitle).HasMaxLength(100);

        builder.Property(u => u.Email)
            .HasMaxLength(100);

        builder.Property(u => u.DateOfBirth)
            .HasColumnType("date");

        builder.Property(u => u.Gender)
            .HasMaxLength(10);

        builder.Property(u => u.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.Property(u => u.LoginByCitizenId).HasDefaultValue(false);
        builder.Property(u => u.RequiresIndividualIdentityConversion).HasDefaultValue(false);

        // Legacy/staff phone logins remain unique. New Patient accounts may share a contact phone and log in by CCCD.
        builder.HasIndex(u => u.Phone)
            .IsUnique()
            .HasFilter("\"LoginByCitizenId\" = false")
            .HasDatabaseName("IX_Users_Phone");

        // Quan hệ N:1 với Role
        builder.HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
