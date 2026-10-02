using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class MedicalHistoryItemConfiguration : IEntityTypeConfiguration<MedicalHistoryItem>
{
    public void Configure(EntityTypeBuilder<MedicalHistoryItem> builder)
    {
        builder.HasKey(item => item.ItemId);

        builder.Property(item => item.Type)
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(item => item.Name)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(item => item.IsCritical)
            .IsRequired();
        builder.Property(item => item.Detail)
            .HasMaxLength(1000);

        builder.HasIndex(item => item.RecordId)
            .HasDatabaseName("IX_MedicalHistoryItems_RecordId");
    }
}
