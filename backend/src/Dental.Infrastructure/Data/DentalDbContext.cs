using System.Text.Json;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data;

/// <summary>DbContext chính của hệ thống phòng khám.</summary>
public sealed class DentalDbContext : DbContext
{
    public DentalDbContext(DbContextOptions<DentalDbContext> options) : base(options) { }

    public DbSet<DentalImage> DentalImages => Set<DentalImage>();
    public DbSet<ClinicRoom> ClinicRooms => Set<ClinicRoom>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientGuardian> PatientGuardians => Set<PatientGuardian>();

    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<ToothCondition> ToothConditions => Set<ToothCondition>();
    public DbSet<VisitService> VisitServices => Set<VisitService>();
    public DbSet<InvoiceRefund> InvoiceRefunds => Set<InvoiceRefund>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<MedicalHistoryRecord> MedicalHistoryRecords => Set<MedicalHistoryRecord>();
    public DbSet<VitalSignRecord> VitalSignRecords => Set<VitalSignRecord>();
    public DbSet<DentalService> DentalServices => Set<DentalService>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    private bool savingHandover;

    internal async Task<int> SavePrescriptionHandoverAsync(CancellationToken ct)
    {
        savingHandover = true;
        try { return await SaveChangesAsync(ct); }
        finally { savingHandover = false; }
    }

    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<MedicalSupply> MedicalSupplies => Set<MedicalSupply>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<InventoryTransactionLine> InventoryTransactionLines => Set<InventoryTransactionLine>();
    public DbSet<DentalDiagnosis> DentalDiagnoses => Set<DentalDiagnosis>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PatientGuardianConfiguration());

        modelBuilder.ApplyConfiguration(new DentalImageConfiguration());
        modelBuilder.ApplyConfiguration(new PrescriptionConfiguration());
        modelBuilder.ApplyConfiguration(new PrescriptionItemConfiguration());
        modelBuilder.ApplyConfiguration(new MedicineConfiguration());
        modelBuilder.ApplyConfiguration(new MedicalSupplyConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryTransactionLineConfiguration());
        modelBuilder.ApplyConfiguration(new DentalDiagnosisConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new SystemConfigConfiguration());
        // Áp dụng từng configuration riêng theo entity
        modelBuilder.ApplyConfiguration(new ClinicRoomConfiguration());
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
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
        modelBuilder.Entity<Invoice>().Property(x => x.RefundRevision).HasDefaultValue(0);
        modelBuilder.ApplyConfiguration(new InvoiceRefundConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceItemConfiguration());
        modelBuilder.ApplyConfiguration(new MedicalHistoryRecordConfiguration());
        modelBuilder.ApplyConfiguration(new VitalSignRecordConfiguration());
        modelBuilder.ApplyConfiguration(new DentalServiceConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
        modelBuilder.ApplyConfiguration(new QueueEntryConfiguration());
    }

    /// <summary>
    /// Ghi đè SaveChanges để bảo vệ tính bất biến của AuditLog:
    /// ném lỗi nếu có AuditLog bị Modified hoặc Deleted.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardStockReceiptImmutability();
        GuardPrescriptionImmutability();
        GuardRefundProposalScope();
        GuardAuditLogImmutability();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        GuardStockReceiptImmutability();
        GuardPrescriptionImmutability();
        GuardRefundProposalScope();
        GuardAuditLogImmutability();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void GuardStockReceiptImmutability()
    {
        if (ChangeTracker.Entries<InventoryTransaction>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<InventoryTransactionLine>().Any(e => e.State is EntityState.Modified or EntityState.Deleted ||
                e.State == EntityState.Added && e.Entity.TransactionId > 0 &&
                !ChangeTracker.Entries<InventoryTransaction>().Any(r => r.State == EntityState.Added && r.Entity.Id == e.Entity.TransactionId)))
            throw new InvalidOperationException("Chứng từ nhập/xuất kho đã xác nhận là bất biến.");
    }

    private void GuardPrescriptionImmutability()
    {
        var parents = ChangeTracker.Entries<Prescription>().Where(e => e.State is EntityState.Modified or EntityState.Deleted).ToArray();
        var protectedIds = new HashSet<int>();
        foreach (var entry in parents)
        {
            var dispProp = entry.Property(p => p.DispenseHistoryJson);
            var origDisp = dispProp.OriginalValue;
            var currDisp = dispProp.CurrentValue;
            var appendedEventOk = dispProp.IsModified &&
                IsAppendOnlyJsonList<PrescriptionDispenseEvent>(origDisp, currDisp, out var oldEvents, out var newEvents) &&
                newEvents.Count == oldEvents.Count + 1 &&
                newEvents[^1].PrescriptionVersion == entry.Entity.Version;

            var handoverOnly = savingHandover && entry.State == EntityState.Modified &&
                !entry.Property(p => p.IsCancelled).OriginalValue &&
                entry.Entity.Version == (long)entry.Property(p => p.Version).OriginalValue + 1 &&
                (entry.Property(p => p.DispensedAt).OriginalValue is null) != (entry.Entity.DispensedAt is null) &&
                entry.Properties.All(p => !p.IsModified || p.Metadata.Name is
                    nameof(Prescription.DispensedAt) or nameof(Prescription.DispensedBy) or
                    nameof(Prescription.ReceivedByProxy) or nameof(Prescription.ReceiverName) or
                    nameof(Prescription.ReceiverPhone) or nameof(Prescription.DispenseNote) or
                    nameof(Prescription.Version) or nameof(Prescription.UpdatedAt) or
                    nameof(Prescription.DispenseHistoryJson)) &&
                appendedEventOk;
            if (entry.State == EntityState.Deleted || entry.Property(p => p.IsCancelled).OriginalValue ||
                entry.Property(p => p.DispensedAt).OriginalValue is not null && !handoverOnly)
                throw new InvalidOperationException("Prescription đã hủy/cấp phát là bất biến; ngoại lệ bàn giao chỉ qua giao dịch DL-298.");
            if (!handoverOnly) protectedIds.Add(entry.Entity.Id);
        }
        foreach (var entry in ChangeTracker.Entries<PrescriptionItem>().Where(e => e.State is EntityState.Modified or EntityState.Deleted or EntityState.Added))
        {
            var id = entry.State == EntityState.Added ? entry.Entity.PrescriptionId : entry.Property(p => p.PrescriptionId).OriginalValue;
            if (id > 0) protectedIds.Add(id);
        }
        if (protectedIds.Count > 0 && Prescriptions.AsNoTracking().Any(p => protectedIds.Contains(p.Id) &&
            (p.IsCancelled || p.DispensedAt != null ||
             Invoices.Any(i => i.VisitId == p.VisitId && (i.PaidAmount != 0 || i.Status == Dental.Domain.Enums.InvoiceStatus.Paid || i.Status == Dental.Domain.Enums.InvoiceStatus.PartiallyPaid || i.Status == Dental.Domain.Enums.InvoiceStatus.PartiallyRefunded || i.Status == Dental.Domain.Enums.InvoiceStatus.Refunded)) ||
             PaymentTransactions.Any(t => t.Invoice.VisitId == p.VisitId))))
            throw new InvalidOperationException("Nội dung đơn thuốc đã phát sinh thanh toán/cấp phát/hủy là bất biến.");
    }

    private void GuardRefundProposalScope()
    {
        var decisionOnlyFields = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(InvoiceRefund.Status), nameof(InvoiceRefund.ReviewerId),
            nameof(InvoiceRefund.DecidedAt), nameof(InvoiceRefund.DecisionNotes)
        };
        var approvalSettlementFields = new HashSet<string>(decisionOnlyFields, StringComparer.Ordinal)
        {
            nameof(InvoiceRefund.Revision), nameof(InvoiceRefund.PayloadHash),
            nameof(InvoiceRefund.PaymentTransactionId), nameof(InvoiceRefund.RefundAmount),
            nameof(InvoiceRefund.DebtReduction), nameof(InvoiceRefund.TotalBefore),
            nameof(InvoiceRefund.DiscountBefore), nameof(InvoiceRefund.PaidBefore),
            nameof(InvoiceRefund.TotalAfter), nameof(InvoiceRefund.DiscountAfter),
            nameof(InvoiceRefund.PaidAfter), nameof(InvoiceRefund.PaymentMethod),
            nameof(InvoiceRefund.LateApproval), nameof(InvoiceRefund.ReserveConfirmed)
        };
        foreach (var entry in ChangeTracker.Entries<InvoiceRefund>().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Lịch sử hoàn tiền chỉ được nối thêm.");

            var originalStatus = entry.Property(x => x.Status).OriginalValue;
            if (originalStatus is not (InvoiceRefundStatuses.Pending or InvoiceRefundStatuses.Stale))
                throw new InvalidOperationException("Lịch sử hoàn tiền chỉ được nối thêm.");

            var nextStatus = entry.Entity.Status;
            var allowed = nextStatus == InvoiceRefundStatuses.Approved ? approvalSettlementFields : decisionOnlyFields;
            if (entry.Properties.Any(p => p.IsModified && !allowed.Contains(p.Metadata.Name)))
                throw new InvalidOperationException("Phạm vi và lý do đề xuất hoàn tiền là bất biến; chỉ trạng thái quyết định được cập nhật.");
        }
    }

    private void GuardAuditLogImmutability()
    {
        var changedInvoiceIds = ChangeTracker.Entries<InvoiceItem>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted or EntityState.Added)
            .Select(e => e.State == EntityState.Added ? e.Entity.InvoiceId : e.Property(x => x.InvoiceId).OriginalValue).Where(id => id > 0).Distinct().ToArray();
        if (changedInvoiceIds.Length > 0 && Invoices.AsNoTracking().Any(i => changedInvoiceIds.Contains(i.Id) &&
            (i.Status == Dental.Domain.Enums.InvoiceStatus.Paid || i.Status == Dental.Domain.Enums.InvoiceStatus.PartiallyPaid ||
             i.Status == Dental.Domain.Enums.InvoiceStatus.PartiallyRefunded || i.Status == Dental.Domain.Enums.InvoiceStatus.Refunded ||
             PaymentTransactions.Any(t => t.InvoiceId == i.Id) ||
             InvoiceRefunds.Any(r => r.InvoiceId == i.Id && r.Status == InvoiceRefundStatuses.Approved))))
            throw new InvalidOperationException("Chi tiết hóa đơn gốc đã thanh toán/hoàn tiền là bất biến.");
        foreach (var entry in ChangeTracker.Entries<Prescription>().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            var dispProp = entry.Property(p => p.DispenseHistoryJson);
            if ((entry.State == EntityState.Deleted && !string.IsNullOrEmpty(dispProp.OriginalValue)) ||
                (dispProp.IsModified && !IsAppendOnlyJsonList<PrescriptionDispenseEvent>(dispProp.OriginalValue, dispProp.CurrentValue)))
                throw new InvalidOperationException("Lịch sử bàn giao thuốc chỉ được nối thêm.");
        }
        // DL-210 allows only a single increment of print metadata, never financial edits.
        if (ChangeTracker.Entries<PaymentTransaction>().Any(x => x.State == EntityState.Deleted ||
            (x.State == EntityState.Modified && (x.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(PaymentTransaction.PrintCount)) ||
             (long)x.Entity.PrintCount != (long)x.Property(p => p.PrintCount).OriginalValue + 1))))
            throw new InvalidOperationException("Dữ liệu khoản thu PaymentTransaction là bất biến; chỉ được tăng PrintCount một lượt theo DL-210.");
        foreach (var entry in ChangeTracker.Entries<Visit>().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            var unlockProp = entry.Property(v => v.UnlockHistoryJson);
            if ((entry.State == EntityState.Deleted && !string.IsNullOrEmpty(unlockProp.OriginalValue)) ||
                (unlockProp.IsModified && !IsAppendOnlyJsonList<VisitUnlockRecord>(unlockProp.OriginalValue, unlockProp.CurrentValue)))
                throw new InvalidOperationException("VisitUnlockRecord là bất biến — không được phép cập nhật hoặc xóa.");
            var noteProp = entry.Property(v => v.PostTreatmentNotesJson);
            if ((entry.State == EntityState.Deleted && !string.IsNullOrEmpty(noteProp.OriginalValue)) ||
                (noteProp.IsModified && !IsAppendOnlyJsonList<PostTreatmentNote>(noteProp.OriginalValue, noteProp.CurrentValue)))
                throw new InvalidOperationException("PostTreatmentNote là ghi chú hậu điều trị chỉ thêm mới — không được sửa hoặc xóa.");
        }
        foreach (var entry in ChangeTracker.Entries<DentalImage>().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            var addendaProp = entry.Property(i => i.ClinicalAddendaJson);
            if ((entry.State == EntityState.Deleted && !string.IsNullOrEmpty(addendaProp.OriginalValue)) ||
                (addendaProp.IsModified && !IsAppendOnlyJsonList<DentalImageClinicalAddendum>(addendaProp.OriginalValue, addendaProp.CurrentValue)))
                throw new InvalidOperationException("Dental image clinical addenda are append-only.");
        }
        foreach (var entry in ChangeTracker.Entries<QueueEntry>().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            var histProp = entry.Property(q => q.StatusHistoryJson);
            if ((entry.State == EntityState.Deleted && !string.IsNullOrEmpty(histProp.OriginalValue)) ||
                (histProp.IsModified && !IsAppendOnlyJsonList<QueueStatusHistory>(histProp.OriginalValue, histProp.CurrentValue)))
                throw new InvalidOperationException("QueueStatusHistory là bất biến — không được phép cập nhật hoặc xóa.");
        }
        var violatingEntries = ChangeTracker.Entries<AuditLog>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (violatingEntries.Count > 0)
        {
            throw new InvalidOperationException(
                "AuditLog là bất biến — không được phép cập nhật hoặc xóa.");
        }

        foreach (var entry in ChangeTracker.Entries<DentalService>().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            var priceProp = entry.Property(s => s.PriceHistoryJson);
            if ((entry.State == EntityState.Deleted && !string.IsNullOrEmpty(priceProp.OriginalValue)) ||
                (priceProp.IsModified && !IsAppendOnlyJsonList<ServicePrice>(priceProp.OriginalValue, priceProp.CurrentValue)))
                throw new InvalidOperationException("ServicePrice là dữ liệu chỉ thêm mới — không được phép cập nhật hoặc xóa.");
        }
    }

    private static bool IsAppendOnlyJsonList<T>(string? originalJson, string? currentJson)
        => IsAppendOnlyJsonList<T>(originalJson, currentJson, out _, out _);

    private static bool IsAppendOnlyJsonList<T>(string? originalJson, string? currentJson, out List<T> oldList, out List<T> newList)
    {
        oldList = string.IsNullOrEmpty(originalJson) ? [] : JsonSerializer.Deserialize<List<T>>(originalJson) ?? [];
        newList = string.IsNullOrEmpty(currentJson) ? [] : JsonSerializer.Deserialize<List<T>>(currentJson) ?? [];
        if (newList.Count < oldList.Count) return false;
        for (var i = 0; i < oldList.Count; i++)
            if (JsonSerializer.Serialize(oldList[i]) != JsonSerializer.Serialize(newList[i])) return false;
        return true;
    }
}
