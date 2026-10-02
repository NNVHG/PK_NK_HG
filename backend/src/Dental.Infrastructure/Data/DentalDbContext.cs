using Dental.Domain.Entities;
using Dental.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data;

/// <summary>DbContext chính — chỉ chứa 3 bảng của Sprint 0: Roles, Users, AuditLogs.</summary>
public sealed class DentalDbContext : DbContext
{
    public DentalDbContext(DbContextOptions<DentalDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<MedicalHistoryRecord> MedicalHistoryRecords => Set<MedicalHistoryRecord>();
    public DbSet<MedicalHistoryItem> MedicalHistoryItems => Set<MedicalHistoryItem>();
    public DbSet<VitalSignRecord> VitalSignRecords => Set<VitalSignRecord>();

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
        modelBuilder.ApplyConfiguration(new MedicalHistoryRecordConfiguration());
        modelBuilder.ApplyConfiguration(new MedicalHistoryItemConfiguration());
        modelBuilder.ApplyConfiguration(new VitalSignRecordConfiguration());
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
    }
}
