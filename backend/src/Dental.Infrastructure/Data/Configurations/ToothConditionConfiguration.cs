using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class ToothConditionConfiguration : IEntityTypeConfiguration<ToothCondition>
{
    public void Configure(EntityTypeBuilder<ToothCondition> builder)
    {
        builder.ToTable("ToothConditions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Surface).HasColumnType("character varying(20)");
        builder.Property(x => x.ConditionCode).HasColumnType("character varying(50)").IsRequired();
        builder.Property(x => x.Note).HasColumnType("character varying(500)");
        builder.HasIndex(x => new { x.VisitId, x.ToothNumber });
        builder.HasOne(x => x.Visit).WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
    }
}
