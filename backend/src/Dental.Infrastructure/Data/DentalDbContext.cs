using Dental.Domain.Entities;
using Dental.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data;

/// <summary>DbContext chính của hệ thống phòng khám.</summary>
public sealed class DentalDbContext : DbContext
{
    public DentalDbContext(DbContextOptions<DentalDbContext> options) : base(options) { }

    public DbSet<StaffWorkspace> StaffWorkspaces => Set<StaffWorkspace>();
    public DbSet<DentalImage> DentalImages => Set<DentalImage>();
    public DbSet<DentalImageClinicalAddendum> DentalImageClinicalAddenda => Set<DentalImageClinicalAddendum>();
    public DbSet<DentalChair> DentalChairs => Set<DentalChair>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<VisitUnlockRecord> VisitUnlockRecords => Set<VisitUnlockRecord>();
    public DbSet<ToothCondition> ToothConditions => Set<ToothCondition>();
    public DbSet<VisitService> VisitServices => Set<VisitService>();
    public DbSet<InvoiceRefund> InvoiceRefunds => Set<InvoiceRefund>();
    public DbSet<InvoiceRefundItem> InvoiceRefundItems => Set<InvoiceRefundItem>();
    public DbSet<InvoiceRefundProposal> InvoiceRefundProposals => Set<InvoiceRefundProposal>();
    public DbSet<InvoiceRefundProposalItem> InvoiceRefundProposalItems => Set<InvoiceRefundProposalItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<DiscountPreset> DiscountPresets => Set<DiscountPreset>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<InvoiceNumberCounter> InvoiceNumberCounters => Set<InvoiceNumberCounter>();
    public DbSet<MedicalHistoryRecord> MedicalHistoryRecords => Set<MedicalHistoryRecord>();
    public DbSet<MedicalHistoryItem> MedicalHistoryItems => Set<MedicalHistoryItem>();
    public DbSet<VitalSignRecord> VitalSignRecords => Set<VitalSignRecord>();
    public DbSet<DentalService> DentalServices => Set<DentalService>();
    public DbSet<ServicePrice> ServicePrices => Set<ServicePrice>();
    public DbSet<BookingRequest> BookingRequests => Set<BookingRequest>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<QueueStatusHistory> QueueStatusHistories => Set<QueueStatusHistory>();
    public DbSet<PrescriptionDispenseEvent> PrescriptionDispenseEvents => Set<PrescriptionDispenseEvent>();
    private bool savingHandover;

    internal async Task<int> SavePrescriptionHandoverAsync(CancellationToken ct)
    {
        savingHandover = true;
        try { return await SaveChangesAsync(ct); }
        finally { savingHandover = false; }
    }

    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<DoctorSignature> DoctorSignatures => Set<DoctorSignature>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<PrescriptionNumberCounter> PrescriptionNumberCounters => Set<PrescriptionNumberCounter>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<MedicineStockBalance> MedicineStockBalances => Set<MedicineStockBalance>();
    public DbSet<MedicineStockReceipt> MedicineStockReceipts => Set<MedicineStockReceipt>();
    public DbSet<MedicineStockIssue> MedicineStockIssues => Set<MedicineStockIssue>();
    public DbSet<MedicalSupply> MedicalSupplies => Set<MedicalSupply>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<StockReceiptLine> StockReceiptLines => Set<StockReceiptLine>();
    public DbSet<StockIssue> StockIssues => Set<StockIssue>();
    public DbSet<StockIssueLine> StockIssueLines => Set<StockIssueLine>();
    public DbSet<DentalDiagnosis> DentalDiagnoses => Set<DentalDiagnosis>();
    public DbSet<PostTreatmentNote> PostTreatmentNotes => Set<PostTreatmentNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DentalImageConfiguration());
        modelBuilder.ApplyConfiguration(new DentalImageClinicalAddendumConfiguration());
        modelBuilder.ApplyConfiguration(new PrescriptionConfiguration());
        modelBuilder.ApplyConfiguration(new PrescriptionDispenseEventConfiguration());
        modelBuilder.ApplyConfiguration(new DoctorSignatureConfiguration());
        modelBuilder.ApplyConfiguration(new PrescriptionItemConfiguration());
        modelBuilder.ApplyConfiguration(new PrescriptionNumberCounterConfiguration());
        modelBuilder.ApplyConfiguration(new MedicineConfiguration());
        modelBuilder.ApplyConfiguration(new MedicineStockBalanceConfiguration());
        modelBuilder.ApplyConfiguration(new MedicineStockReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new MedicineStockIssueConfiguration());
        modelBuilder.ApplyConfiguration(new MedicalSupplyConfiguration());
        modelBuilder.ApplyConfiguration(new StockBalanceConfiguration());
        modelBuilder.ApplyConfiguration(new StockReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new StockReceiptLineConfiguration());
        modelBuilder.ApplyConfiguration(new StockIssueConfiguration());
        modelBuilder.ApplyConfiguration(new StockIssueLineConfiguration());
        modelBuilder.ApplyConfiguration(new DentalDiagnosisConfiguration());
        modelBuilder.ApplyConfiguration(new PostTreatmentNoteConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new SystemConfigConfiguration());
        modelBuilder.ApplyConfiguration(new VisitUnlockRecordConfiguration());
        // Áp dụng từng configuration riêng theo entity
        modelBuilder.ApplyConfiguration(new BookingRequestConfiguration());
        modelBuilder.ApplyConfiguration(new StaffWorkspaceConfiguration());
        modelBuilder.ApplyConfiguration(new DentalChairConfiguration());
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
        modelBuilder.ApplyConfiguration(new InvoiceRefundItemConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceRefundProposalConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceRefundProposalItemConfiguration());
        modelBuilder.ApplyConfiguration(new DiscountPresetConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceItemConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceNumberCounterConfiguration());
        modelBuilder.ApplyConfiguration(new MedicalHistoryRecordConfiguration());
        modelBuilder.ApplyConfiguration(new MedicalHistoryItemConfiguration());
        modelBuilder.ApplyConfiguration(new VitalSignRecordConfiguration());
        modelBuilder.ApplyConfiguration(new DentalServiceConfiguration());
        modelBuilder.ApplyConfiguration(new ServicePriceConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationLogConfiguration());
        modelBuilder.ApplyConfiguration(new QueueEntryConfiguration());
        modelBuilder.ApplyConfiguration(new QueueStatusHistoryConfiguration());
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
        if (ChangeTracker.Entries<MedicineStockReceipt>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<MedicineStockIssue>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Chứng từ tồn thuốc đã xác nhận là bất biến.");
        if (ChangeTracker.Entries<StockIssue>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<StockIssueLine>().Any(e => e.State is EntityState.Modified or EntityState.Deleted ||
                e.State == EntityState.Added && e.Entity.StockIssueId > 0 &&
                !ChangeTracker.Entries<StockIssue>().Any(r => r.State == EntityState.Added && r.Entity.Id == e.Entity.StockIssueId)))
            throw new InvalidOperationException("Phiếu xuất đã xác nhận là bất biến.");
        if (ChangeTracker.Entries<StockReceipt>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<StockReceiptLine>().Any(e => e.State is EntityState.Modified or EntityState.Deleted ||
                e.State == EntityState.Added && e.Entity.StockReceiptId > 0 &&
                !ChangeTracker.Entries<StockReceipt>().Any(r => r.State == EntityState.Added && r.Entity.Id == e.Entity.StockReceiptId)))
            throw new InvalidOperationException("Phiếu nhập đã xác nhận là bất biến.");
    }

    private void GuardPrescriptionImmutability()
    {
        var parents = ChangeTracker.Entries<Prescription>().Where(e => e.State is EntityState.Modified or EntityState.Deleted).ToArray();
        var protectedIds = new HashSet<int>();
        foreach (var entry in parents)
        {
            var handoverOnly = savingHandover && entry.State == EntityState.Modified &&
                !entry.Property(p => p.IsCancelled).OriginalValue &&
                entry.Entity.Version == (long)entry.Property(p => p.Version).OriginalValue + 1 &&
                (entry.Property(p => p.DispensedAt).OriginalValue is null) != (entry.Entity.DispensedAt is null) &&
                entry.Properties.All(p => !p.IsModified || p.Metadata.Name is
                    nameof(Prescription.DispensedAt) or nameof(Prescription.DispensedBy) or
                    nameof(Prescription.ReceivedByProxy) or nameof(Prescription.ReceiverName) or
                    nameof(Prescription.ReceiverPhone) or nameof(Prescription.DispenseNote) or
                    nameof(Prescription.Version) or nameof(Prescription.UpdatedAt)) &&
                ChangeTracker.Entries<PrescriptionDispenseEvent>().Any(e => e.State == EntityState.Added &&
                    e.Entity.PrescriptionId == entry.Entity.Id && e.Entity.PrescriptionVersion == entry.Entity.Version);
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
        var decisionFields = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(InvoiceRefundProposal.Status), nameof(InvoiceRefundProposal.ReviewerId),
            nameof(InvoiceRefundProposal.DecidedAt), nameof(InvoiceRefundProposal.DecisionNotes),
            nameof(InvoiceRefundProposal.RefundId)
        };
        foreach (var entry in ChangeTracker.Entries<InvoiceRefundProposal>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified &&
                entry.Properties.Any(p => p.IsModified && !decisionFields.Contains(p.Metadata.Name)))
                throw new InvalidOperationException("Phạm vi và lý do đề xuất hoàn tiền là bất biến; chỉ trạng thái quyết định được cập nhật.");
        }
        if (ChangeTracker.Entries<InvoiceRefundProposalItem>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Dòng và số lượng trong đề xuất hoàn tiền là bất biến.");
    }

    private void GuardAuditLogImmutability()
    {
        var changedInvoiceIds = ChangeTracker.Entries<InvoiceItem>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted or EntityState.Added)
            .Select(e => e.State == EntityState.Added ? e.Entity.InvoiceId : e.Property(x => x.InvoiceId).OriginalValue).Where(id => id > 0).Distinct().ToArray();
        if (changedInvoiceIds.Length > 0 && Invoices.AsNoTracking().Any(i => changedInvoiceIds.Contains(i.Id) &&
            (i.Status == Dental.Domain.Enums.InvoiceStatus.Paid || i.Status == Dental.Domain.Enums.InvoiceStatus.PartiallyPaid ||
             i.Status == Dental.Domain.Enums.InvoiceStatus.PartiallyRefunded || i.Status == Dental.Domain.Enums.InvoiceStatus.Refunded ||
             PaymentTransactions.Any(t => t.InvoiceId == i.Id) || InvoiceRefunds.Any(r => r.InvoiceId == i.Id))))
            throw new InvalidOperationException("Chi tiết hóa đơn gốc đã thanh toán/hoàn tiền là bất biến.");
        if (ChangeTracker.Entries<InvoiceRefund>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<InvoiceRefundItem>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Lịch sử hoàn tiền chỉ được nối thêm.");
        if (ChangeTracker.Entries<PrescriptionDispenseEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Lịch sử bàn giao thuốc chỉ được nối thêm.");
        if (ChangeTracker.Entries<DoctorSignature>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Phiên bản ảnh chữ ký chỉ thêm mới; không sửa hoặc xóa lịch sử.");
        if (ChangeTracker.Entries<NotificationLog>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("NotificationLog là nhật ký chỉ thêm mới — không được sửa hoặc xóa.");
        // DL-210 allows only a single increment of print metadata, never financial edits.
        if (ChangeTracker.Entries<PaymentTransaction>().Any(x => x.State == EntityState.Deleted ||
            (x.State == EntityState.Modified && (x.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(PaymentTransaction.PrintCount)) ||
             (long)x.Entity.PrintCount != (long)x.Property(p => p.PrintCount).OriginalValue + 1))))
            throw new InvalidOperationException("Dữ liệu khoản thu PaymentTransaction là bất biến; chỉ được tăng PrintCount một lượt theo DL-210.");
        if (ChangeTracker.Entries<VisitUnlockRecord>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("VisitUnlockRecord là bất biến — không được phép cập nhật hoặc xóa.");
        if (ChangeTracker.Entries<PostTreatmentNote>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("PostTreatmentNote là ghi chú hậu điều trị chỉ thêm mới — không được sửa hoặc xóa.");
        if (ChangeTracker.Entries<DentalImageClinicalAddendum>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Dental image clinical addenda are append-only.");
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
