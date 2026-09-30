using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.LogId);

        builder.Property(a => a.Action)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Detail)
            .HasColumnType("text"); // JSON text — đủ cho Sprint 0; có thể đổi sang jsonb sau

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        builder.Property(a => a.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        // Index cho tra cứu audit theo entity (mục 4 DATABASE_DESIGN.md)
        builder.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("IX_AuditLogs_Entity");

        // Index cho tra cứu theo user + thời gian
        builder.HasIndex(a => new { a.UserId, a.CreatedAt })
            .HasDatabaseName("IX_AuditLogs_User");

        // Quan hệ tùy chọn với User (UserId nullable — log khi chưa đăng nhập)
        builder.HasOne(a => a.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
