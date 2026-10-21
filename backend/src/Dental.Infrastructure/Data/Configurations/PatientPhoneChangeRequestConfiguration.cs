using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dental.Infrastructure.Data.Configurations;
public sealed class PatientPhoneChangeRequestConfiguration : IEntityTypeConfiguration<PatientPhoneChangeRequest>
{
    public void Configure(EntityTypeBuilder<PatientPhoneChangeRequest> b)
    {
        b.ToTable("PatientPhoneChangeRequests");
        b.HasKey(x => x.Id);
        b.Property(x => x.CurrentPhone).HasMaxLength(10).IsRequired();
        b.Property(x => x.NewPhone).HasMaxLength(10).IsRequired();
        b.HasIndex(x => new { x.UserId, x.TokenVersion }).IsUnique().HasFilter("\"CompletedAtUtc\" IS NULL");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
