using System.Text.Json;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Explicit opt-in; synthetic data and a unique disposable database, never the clinic database.
if (args.Length is not (2 or 3) || args[0] != "--isolated-config" ||
    args.Length == 3 && args[2] is not ("--invoice-refunds-only" or "--invoice-draft-cancellation-only" or "--queue-call-next-only" or "--invoice-discounts-only" or "--service-catalog-only" or "--queue-imaging-only" or "--queue-calling-only" or "--queue-absence-only" or "--queue-cleanup-only" or "--chair-catalog-only" or "--staff-workspaces-only" or "--queue-locations-only" or "--visit-corrections-only" or "--physical-chairs-only" or "--visit-cancellation-only" or "--post-treatment-notes-only" or "--diagnosis-catalog-only" or "--revenue-report-only" or "--medicine-catalog-only" or "--prescription-only" or "--patient-citizen-id-only" or "--public-prescription-only" or "--doctor-signature-only" or "--prescription-replacement-only" or "--prescription-print-only" or "--prescription-handover-only" or "--patient-prescription-history-only" or "--medical-supply-catalog-only" or "--password-recovery-only" or "--dental-images-only" or "--stock-receipts-only" or "--stock-issues-only"))
    throw new ArgumentException("Use --isolated-config <config.json> [--invoice-refunds-only|--invoice-draft-cancellation-only|--queue-call-next-only|--invoice-discounts-only|--service-catalog-only|--queue-imaging-only|--queue-calling-only|--queue-absence-only|--queue-cleanup-only|--chair-catalog-only|--staff-workspaces-only|--queue-locations-only|--visit-corrections-only|--physical-chairs-only|--visit-cancellation-only|--post-treatment-notes-only|--diagnosis-catalog-only|--revenue-report-only|--medicine-catalog-only|--prescription-only|--patient-citizen-id-only|--public-prescription-only|--doctor-signature-only|--prescription-replacement-only|--prescription-print-only|--prescription-handover-only|--patient-prescription-history-only|--medical-supply-catalog-only|--password-recovery-only|--dental-images-only|--stock-receipts-only|--stock-issues-only]");
var prescriptionOnly = args.Length == 3 && args[2] is "--prescription-only" or "--patient-citizen-id-only" or "--public-prescription-only" or "--doctor-signature-only" or "--prescription-replacement-only" or "--prescription-print-only" or "--prescription-handover-only" or "--patient-prescription-history-only";
var medicineCatalogOnly = args.Length == 3 && args[2] == "--medicine-catalog-only";
var revenueReportOnly = args.Length == 3 && args[2] == "--revenue-report-only";
var medicalSupplyCatalogOnly = args.Length == 3 && args[2] == "--medical-supply-catalog-only";
var diagnosisCatalogOnly = args.Length == 3 && args[2] == "--diagnosis-catalog-only";
var postTreatmentNotesOnly = args.Length == 3 && args[2] == "--post-treatment-notes-only";
var queueCallNextOnly = args.Length == 3 && args[2] == "--queue-call-next-only";
var invoiceDiscountsOnly = args.Length == 3 && args[2] == "--invoice-discounts-only";
var invoiceDraftCancellationOnly = args.Length == 3 && args[2] == "--invoice-draft-cancellation-only";
var serviceCatalogOnly = args.Length == 3 && args[2] == "--service-catalog-only";
var queueImagingOnly = args.Length == 3 && args[2] == "--queue-imaging-only";
var visitCancellationOnly = args.Length == 3 && args[2] == "--visit-cancellation-only";
var physicalChairsOnly = args.Length == 3 && args[2] == "--physical-chairs-only";
var visitCorrectionsOnly = args.Length == 3 && args[2] == "--visit-corrections-only";
var queueLocationsOnly = args.Length == 3 && args[2] == "--queue-locations-only";
var staffWorkspacesOnly = args.Length == 3 && args[2] == "--staff-workspaces-only";
var chairCatalogOnly = args.Length == 3 && args[2] == "--chair-catalog-only";
var queueCleanupOnly = args.Length == 3 && args[2] == "--queue-cleanup-only";
var queueAbsenceOnly = args.Length == 3 && args[2] == "--queue-absence-only";
var queueCallingOnly = args.Length == 3 && args[2] == "--queue-calling-only";
using var config = JsonDocument.Parse(File.ReadAllText(args[1]));
var connection = new NpgsqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString());
var databaseName = "dental_cash_test_" + Guid.NewGuid().ToString("N");
connection.Database = databaseName;
var options = new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql(connection.ConnectionString).Options;
DentalDbContext NewDb() => new(options);
var checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }
await using var setup = NewDb();
try
{
    await setup.Database.MigrateAsync();
    if (args.Length == 3 && args[2] == "--stock-issues-only")
    {
        await StockIssueHttpSmoke.RunAsync(options, Check);
        Console.WriteLine($"PostgreSQL stock issue checks: {checks}/{checks} PASS");
        return;
    }
    if (args.Length == 3 && args[2] == "--stock-receipts-only")
    {
        await StockReceiptHttpSmoke.RunAsync(options, Check);
        Console.WriteLine($"PostgreSQL stock receipt checks: {checks}/{checks} PASS");
        return;
    }
    if (args.Length == 3 && args[2] == "--dental-images-only")
    {
        await DentalImageHttpSmoke.RunAsync(options, Check);
        Console.WriteLine($"PostgreSQL image upload checks: {checks}/{checks} PASS");
        return;
    }
    if (args.Length == 3 && args[2] == "--password-recovery-only")
    {
        await PasswordRecoveryHttpSmoke.RunAsync(options, Check);
        Console.WriteLine($"PostgreSQL password recovery checks: {checks}/{checks} PASS");
        return;
    }
    var role = new Role { RoleCode = "RECEPTIONIST", RoleName = "Test cashier" };
    var cashier = new User { Role = role, FullName = "Synthetic cashier", Phone = "0999999991", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
    var patient = new Patient { FullName = "Synthetic patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0999999992" };
    var visit = new Visit { Patient = patient, CreatedByUser = cashier, Status = VisitStatuses.Completed, IsLocked = true };
    var invoice = new Invoice { Visit = visit, CreatedByUser = cashier, InvoiceCode = "TEST-CASH-1", TotalAmount = 100000, Status = InvoiceStatus.PendingPayment, CreatedAt = DateTime.UtcNow };
    setup.Invoices.Add(invoice); await setup.SaveChangesAsync(); setup.ChangeTracker.Clear();
    if (args.Length==3 && args[2]=="--invoice-refunds-only")
    {
        await CashPaymentHttpSmoke.RunAsync(options,cashier.UserId,patient.PatientId,Check,invoiceRefundsOnly:true);
        Console.WriteLine($"PostgreSQL invoice refunds checks: {checks}/{checks} PASS");return;
    }
    if (invoiceDraftCancellationOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, invoiceDraftCancellationOnly: true);
        Console.WriteLine($"PostgreSQL invoice Draft cancellation checks: {checks}/{checks} PASS");
        return;
    }
    if (prescriptionOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, prescriptionOnly: true);
        Console.WriteLine($"PostgreSQL {(args[2] == "--prescription-print-only" ? "prescription print" : args[2] == "--prescription-replacement-only" ? "prescription replacement" : args[2] == "--doctor-signature-only" ? "doctor signatures" : args[2] == "--patient-citizen-id-only" ? "patient citizen identity" : args[2] == "--public-prescription-only" ? "public prescription" : "prescription")} checks: {checks}/{checks} PASS");
        return;
    }
    if (medicalSupplyCatalogOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, medicalSupplyCatalogOnly: true);
        Console.WriteLine($"PostgreSQL medical supply catalog checks: {checks}/{checks} PASS");
        return;
    }
    if (medicineCatalogOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, medicineCatalogOnly: true);
        Console.WriteLine($"PostgreSQL medicine catalog checks: {checks}/{checks} PASS");
        return;
    }
    if (revenueReportOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, revenueReportOnly: true);
        Console.WriteLine($"PostgreSQL revenue report checks: {checks}/{checks} PASS");
        return;
    }
    if (diagnosisCatalogOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, diagnosisCatalogOnly: true);
        Console.WriteLine($"PostgreSQL diagnosis catalog checks: {checks}/{checks} PASS");
        return;
    }
    if (postTreatmentNotesOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, postTreatmentNotesOnly: true);
        Console.WriteLine($"PostgreSQL post-treatment checks: {checks}/{checks} PASS");
        return;
    }
    if (visitCancellationOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, visitCancellationOnly: true);
        Console.WriteLine($"PostgreSQL visit cancellation checks: {checks}/{checks} PASS");
        return;
    }
    if (physicalChairsOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, physicalChairsOnly: true);
        Console.WriteLine($"PostgreSQL physical chair checks: {checks}/{checks} PASS");
        return;
    }
    if (visitCorrectionsOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, visitCorrectionsOnly: true);
        Console.WriteLine($"PostgreSQL visit correction checks: {checks}/{checks} PASS");
        return;
    }
    if (queueLocationsOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, queueLocationsOnly: true);
        Console.WriteLine($"PostgreSQL queue location checks: {checks}/{checks} PASS");
        return;
    }
    if (staffWorkspacesOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, staffWorkspacesOnly: true);
        Console.WriteLine($"PostgreSQL staff workspaces checks: {checks}/{checks} PASS");
        return;
    }
    if (chairCatalogOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, chairCatalogOnly: true);
        Console.WriteLine($"PostgreSQL chair catalog checks: {checks}/{checks} PASS");
        return;
    }
    if (queueCleanupOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, queueCleanupOnly: true);
        Console.WriteLine($"PostgreSQL overnight cleanup checks: {checks}/{checks} PASS");
        return;
    }
    if (queueAbsenceOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, queueAbsenceOnly: true);
        Console.WriteLine($"PostgreSQL absence checks: {checks}/{checks} PASS");
        return;
    }
    if (queueCallingOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, queueCallingOnly: true);
        Console.WriteLine($"PostgreSQL Calling checks: {checks}/{checks} PASS");
        return;
    }
    if (queueImagingOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, queueImagingOnly: true);
        Console.WriteLine($"PostgreSQL imaging-routing checks: {checks}/{checks} PASS");
        return;
    }
    if (serviceCatalogOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, serviceCatalogOnly: true);
        Console.WriteLine($"PostgreSQL service-catalog checks: {checks}/{checks} PASS");
        return;
    }
    if (invoiceDiscountsOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, invoiceDiscountsOnly: true);
        Console.WriteLine($"PostgreSQL invoice-discount checks: {checks}/{checks} PASS");
        return;
    }
    if (queueCallNextOnly)
    {
        await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check, queueCallNextOnly: true);
        await QueueCallNextAtomicSmoke.RunAsync(options, cashier.UserId, Check);
        Console.WriteLine($"PostgreSQL call-next checks: {checks}/{checks} PASS");
        return;
    }
    async Task<Dental.Application.Common.Result<PaymentTransaction>> Receive(CashPaymentRequest request)
    {
        await using var db = NewDb();
        return await new CashPaymentRepository(db).ReceiveAsync(invoice.Id, cashier.UserId, request, null);
    }
    var first = new CashPaymentRequest(40000, 50000, Guid.NewGuid());
    var result = await Receive(first);
    Check(result.IsSuccess && result.Value!.ChangeAmount == 10000 && result.Value.CashierId == cashier.UserId, "Cash/change/cashier persisted");
    var replay = await Receive(first);
    Check(replay.IsSuccess && replay.Value!.Id == result.Value!.Id, "Same request replays same receipt");
    Check((await Receive(first with { Amount = 30000 })).IsFailure, "Key reuse with different amount rejected");
    var partial = await setup.Invoices.AsNoTracking().SingleAsync(x => x.Id == invoice.Id);
    Check(partial.PaidAmount == 40000 && partial.Status == InvoiceStatus.PartiallyPaid, "Partial balance/status");
    Check((await Receive(new CashPaymentRequest(60001, 70000, Guid.NewGuid()))).IsFailure, "Over-debt rejected");
    var races = await Task.WhenAll(Receive(new CashPaymentRequest(60000, 100000, Guid.NewGuid())), Receive(new CashPaymentRequest(60000, 100000, Guid.NewGuid())));
    Check(races.Count(x => x.IsSuccess) == 1, "Concurrent charges: exactly one succeeds");
    var paid = await setup.Invoices.AsNoTracking().SingleAsync(x => x.Id == invoice.Id);
    Check(paid.PaidAmount == 100000 && paid.Status == InvoiceStatus.Paid, "Full balance/status; never overpaid");
    Check((await Receive(first)).IsSuccess, "Replay remains successful after invoice Paid");
    Check(await setup.PaymentTransactions.CountAsync() == 2, "Only two immutable receipts");
    Check(await setup.AuditLogs.CountAsync(x => x.Action == "MOD_BIL_CASH_RECEIVED") == 2, "Exactly one audit per committed charge");
    await using var reopenDb = NewDb();
    Check((await new VisitReopenRepository(reopenDb).ReopenAsync(visit.VisitId, cashier.UserId, "Synthetic correction reason", null)).IsFailure, "Paid visit cannot reopen");
    await CashPaymentHttpSmoke.RunAsync(options, cashier.UserId, patient.PatientId, Check);
    await QueueCallNextAtomicSmoke.RunAsync(options, cashier.UserId, Check);
    await CashConflictRollbackSmoke.RunAsync(options, patient.PatientId, cashier.UserId, Check);
    Console.WriteLine($"PostgreSQL payment checks: {checks}/{checks} PASS");
}
finally
{
    if (setup.Database.GetDbConnection().Database != databaseName || !databaseName.StartsWith("dental_cash_test_"))
        throw new InvalidOperationException("Refuse cleanup outside owned test database");
    await setup.Database.EnsureDeletedAsync();
    Console.WriteLine("Owned isolated test database removed");
}
