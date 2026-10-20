using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Dental.Infrastructure.Data.Configurations;
public sealed class BookingRequestConfiguration : IEntityTypeConfiguration<BookingRequest>
{
    public void Configure(EntityTypeBuilder<BookingRequest> b)
    {
        b.ToTable("BookingRequests", t => {
            t.HasCheckConstraint("CK_BookingRequests_Duration", "\"SlotDurationMinutes\" BETWEEN 1 AND 1440");
            t.HasCheckConstraint("CK_BookingRequests_Version", "\"Version\" > 0");
        });
        b.HasKey(x=>x.Id);b.HasIndex(x=>x.ClientRequestId).IsUnique();
        b.Property(x=>x.IntentDigest).HasMaxLength(64).IsRequired();
        b.Property(x=>x.FullName).HasMaxLength(100).IsRequired();b.Property(x=>x.Phone).HasMaxLength(20).IsRequired();
        b.Property(x=>x.Gender).HasMaxLength(10).IsRequired();b.Property(x=>x.Email).HasMaxLength(100);b.Property(x=>x.Notes).HasMaxLength(500);
        b.Property(x=>x.Status).HasMaxLength(20).IsRequired();b.Property(x=>x.RejectionReason).HasMaxLength(500);
        b.Property(x=>x.Version).IsConcurrencyToken();
        b.HasIndex(x=>new{x.Status,x.AppointmentDate,x.SlotTime});
        b.HasOne<User>().WithMany().HasForeignKey(x=>x.DentistId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(x=>x.VerifiedPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x=>x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x=>x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
