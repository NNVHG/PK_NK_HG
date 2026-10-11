using Dental.Domain.Entities;
using Dental.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data;

/// <summary>DbContext chính của hệ thống phòng khám.</summary>
public sealed class DentalDbContext : DbContext
{
    public DentalDbContext(DbContextOptions<DentalDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<ToothCondition> ToothConditions => Set<ToothCondition>();
    public DbSet<VisitService> VisitServices => Set<VisitService>();
    public DbSet<MedicalHistoryRecord> MedicalHistoryRecords => Set<MedicalHistoryRecord>();
    public DbSet<MedicalHistoryItem> MedicalHistoryItems => Set<MedicalHistoryItem>();
    public DbSet<VitalSignRecord> VitalSignRecords => Set<VitalSignRecord>();
    public DbSet<DentalService> DentalServices => Set<DentalService>();
    public DbSet<ServicePrice> ServicePrices => Set<ServicePrice>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<QueueStatusHistory> QueueStatusHistories => Set<QueueStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Áp dụng từng configuration riêng theo entity
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.HasSequence<int>("patient_number_seq")
            .StartsAt(1)
            .IncrementsBy(1)
            .HasMax(999999);
        modelBuilder.ApplyConfiguration(new PatientConfiguration());
        modelBuilder.ApplyConfiguration(new VisitConfiguration());
        modelBuilder.ApplyConfiguration(new ToothConditionConfiguration());
        modelBuilder.ApplyConfiguration(new VisitServiceConfiguration());
        modelBuilder.ApplyConfiguration(new MedicalHistoryRecordConfiguration());
        modelBuilder.ApplyConfiguration(new MedicalHistoryItemConfiguration());
        modelBuilder.ApplyConfiguration(new VitalSignRecordConfiguration());
        modelBuilder.ApplyConfiguration(new DentalServiceConfiguration());
        modelBuilder.ApplyConfiguration(new ServicePriceConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
        modelBuilder.ApplyConfiguration(new QueueEntryConfiguration());
        modelBuilder.ApplyConfiguration(new QueueStatusHistoryConfiguration());
    }

    /// <summary>
    /// Ghi đè SaveChanges để bảo vệ tính bất biến của AuditLog:
    /// ném lỗi nếu có AuditLog bị Modified hoặc Deleted.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardAuditLogImmutability();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        GuardAuditLogImmutability();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void GuardAuditLogImmutability()
    {
        var violatingEntries = ChangeTracker.Entries<AuditLog>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (violatingEntries.Count > 0)
        {
            throw new InvalidOperationException(
                "AuditLog là bất biến — không được phép cập nhật hoặc xóa.");
        }

        var violatingPrices = ChangeTracker.Entries<ServicePrice>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (violatingPrices.Count > 0)
        {
            throw new InvalidOperationException(
                "ServicePrice là dữ liệu chỉ thêm mới — không được phép cập nhật hoặc xóa.");
        }

        var violatingHistories = ChangeTracker.Entries<QueueStatusHistory>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (violatingHistories.Count > 0)
        {
            throw new InvalidOperationException(
                "QueueStatusHistory là bất biến — không được phép cập nhật hoặc xóa.");
        }
    }
}
