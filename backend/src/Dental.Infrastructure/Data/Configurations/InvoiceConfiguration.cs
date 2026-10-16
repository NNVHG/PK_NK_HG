using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("Invoices"); b.HasKey(x => x.Id);
        b.Property(x => x.InvoiceCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.TotalAmount).HasPrecision(18, 0);
        b.Property(x => x.PaidAmount).HasPrecision(18, 0);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 0).HasDefaultValue(0m);
        b.Property(x => x.DiscountType).HasMaxLength(20);
        b.Property(x => x.DiscountValue).HasPrecision(20, 2);
        b.Property(x => x.DiscountReason).HasMaxLength(255);
        b.Ignore(x => x.FinalAmount);
        b.HasOne<DiscountPreset>().WithMany().HasForeignKey(x => x.DiscountPresetId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("CK_Invoices_DiscountAmount", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"TotalAmount\""));
        b.HasIndex(x => x.InvoiceCode).IsUnique();
        b.HasIndex(x => x.VisitId).IsUnique().HasFilter("\"Status\" <> 4");
        b.HasOne(x => x.Visit).WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> b)
    {
        b.ToTable("InvoiceItems"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ItemType).HasMaxLength(20).IsRequired();
        b.Property(x => x.Surface).HasMaxLength(10);
        b.Property(x => x.UnitPrice).HasPrecision(18, 0);
        b.Property(x => x.TotalAmount).HasPrecision(18, 0);
        b.HasOne(x => x.Invoice).WithMany(x => x.Items).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InvoiceNumberCounterConfiguration : IEntityTypeConfiguration<InvoiceNumberCounter>
{
    public void Configure(EntityTypeBuilder<InvoiceNumberCounter> b)
    {
        b.ToTable("InvoiceNumberCounters"); b.HasKey(x => x.InvoiceDate);
    }
}
