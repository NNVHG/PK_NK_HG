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
if (args.Length != 2 || args[0] != "--isolated-config")
    throw new ArgumentException("Use --isolated-config <appsettings.Development.json>");
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
    var role = new Role { RoleCode = "RECEPTIONIST", RoleName = "Test cashier" };
    var cashier = new User { Role = role, FullName = "Synthetic cashier", Phone = "0999999991", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
    var patient = new Patient { FullName = "Synthetic patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0999999992" };
    var visit = new Visit { Patient = patient, CreatedByUser = cashier, Status = VisitStatuses.Completed, IsLocked = true };
    var invoice = new Invoice { Visit = visit, CreatedByUser = cashier, InvoiceCode = "TEST-CASH-1", TotalAmount = 100000, Status = InvoiceStatus.PendingPayment, CreatedAt = DateTime.UtcNow };
    setup.Invoices.Add(invoice); await setup.SaveChangesAsync(); setup.ChangeTracker.Clear();
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
    Console.WriteLine($"PostgreSQL payment checks: {checks}/{checks} PASS");
}
finally
{
    if (setup.Database.GetDbConnection().Database != databaseName || !databaseName.StartsWith("dental_cash_test_"))
        throw new InvalidOperationException("Refuse cleanup outside owned test database");
    await setup.Database.EnsureDeletedAsync();
    Console.WriteLine("Owned isolated test database removed");
}
