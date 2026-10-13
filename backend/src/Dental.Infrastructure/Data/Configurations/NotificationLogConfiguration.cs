using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dental.Infrastructure.Data.Configurations;

public sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> b)
    {
        b.ToTable("NotificationLogs"); b.HasKey(x => x.Id);
        b.Property(x => x.NotificationType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Channel).HasMaxLength(20).IsRequired();
        b.Property(x => x.RecipientPhone).HasMaxLength(20).IsRequired();
        b.Property(x => x.MessageContent).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.AppointmentDate).HasColumnType("date");
        b.Property(x => x.SlotTime).HasColumnType("time");
        b.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.AppointmentId, x.NotificationType });
        b.HasIndex(x => new { x.AppointmentId, x.NotificationType, x.AppointmentDate, x.SlotTime })
            .IsUnique().HasDatabaseName("IX_NotificationLogs_ReminderOccurrence");
    }
}
