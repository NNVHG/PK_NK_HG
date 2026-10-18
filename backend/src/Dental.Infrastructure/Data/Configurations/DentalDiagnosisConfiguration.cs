using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dental.Infrastructure.Data.Configurations;
public sealed class DentalDiagnosisConfiguration : IEntityTypeConfiguration<DentalDiagnosis>
{
    public void Configure(EntityTypeBuilder<DentalDiagnosis> b)
    {
        b.ToTable("DentalDiagnoses"); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Category).HasMaxLength(100).IsRequired(); b.Property(x => x.SearchText).HasMaxLength(600).IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
    }
}
