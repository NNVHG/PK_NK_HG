using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dental.Infrastructure.Data.Configurations;

public sealed class StockIssueConfiguration : IEntityTypeConfiguration<StockIssue>
{
    public void Configure(EntityTypeBuilder<StockIssue> b)
    {
        b.ToTable("StockIssues");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => new { x.VisitId, x.Id });
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne(x => x.Issue).HasForeignKey(x => x.StockIssueId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StockIssueLineConfiguration : IEntityTypeConfiguration<StockIssueLine>
{
    public void Configure(EntityTypeBuilder<StockIssueLine> b)
    {
        b.ToTable("StockIssueLines", t => t.HasCheckConstraint("CK_StockIssueLine_Quantity", "\"Quantity\" > 0"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.StockIssueId, x.MedicalSupplyId }).IsUnique();
        b.Property(x => x.SupplyCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.SupplyName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(50).IsRequired();
        b.HasOne<MedicalSupply>().WithMany().HasForeignKey(x => x.MedicalSupplyId).OnDelete(DeleteBehavior.Restrict);
    }
}
