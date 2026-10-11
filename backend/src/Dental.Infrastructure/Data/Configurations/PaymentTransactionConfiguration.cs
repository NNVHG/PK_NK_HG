using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("PaymentTransactions"); b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 0);
        b.Property(x => x.AmountTendered).HasPrecision(18, 0);
        b.Property(x => x.ChangeAmount).HasPrecision(18, 0);
        b.Property(x => x.PaymentMethod).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
    }
}
