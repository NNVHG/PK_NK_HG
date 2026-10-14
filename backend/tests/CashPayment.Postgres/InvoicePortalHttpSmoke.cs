using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
internal static class InvoicePortalHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actor,
        Action<string> authenticate, Action<string, int> authenticateActor, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Portal fixtures require owned isolated database.");
        var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleCode == RoleCodes.Patient)
            ?? new Role { RoleCode = RoleCodes.Patient, RoleName = "Synthetic portal patient" };
        User Owner(string name) => new() { FullName = name, Role = role, Phone = "07" + Random.Shared.Next(10000000, 99999999),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), IsActive = true };
        var owner = Owner("Synthetic portal account A"); var other = Owner("Synthetic portal account B");
        db.Users.AddRange(owner, other); await db.SaveChangesAsync();
        var sharedPhone = "03" + Random.Shared.Next(10000000, 99999999);
        Patient Profile(string name, User user, bool active = true) => new() { FullName = name, UserId = user.UserId, Phone = sharedPhone,
            DateOfBirth = new DateOnly(2000, 1, 1), IsActive = active };
        var a = Profile("Synthetic family A", owner); var b = Profile("Synthetic family B", owner);
        var foreign = Profile("Synthetic foreign", other); var inactive = Profile("Synthetic archived", owner, false);
        db.Patients.AddRange(a, b, foreign, inactive); await db.SaveChangesAsync();
        var now = DateTime.UtcNow; var prefix = "PORTAL-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Invoice Make(string code, Patient patient, InvoiceStatus status, decimal paid = 0) => new() { InvoiceCode = prefix + code,
            TotalAmount = 100000, PaidAmount = paid, Status = status, CreatedAt = now, CreatedByUserId = actor,
            Visit = new Visit { PatientId = patient.PatientId, CreatedByUserId = actor, Status = VisitStatuses.Completed, IsLocked = true } };
        var invoice = Make("-A", a, InvoiceStatus.PartiallyPaid, 70000); var family = Make("-B", b, InvoiceStatus.PendingPayment);
        var outsider = Make("-F", foreign, InvoiceStatus.Paid, 100000); var archived = Make("-I", inactive, InvoiceStatus.PendingPayment);
        var cancelled = Make("-OLD", a, InvoiceStatus.Cancelled); cancelled.Visit = invoice.Visit; cancelled.CreatedAt = now.AddDays(-1);
        db.Invoices.AddRange(invoice, family, outsider, archived, cancelled); await db.SaveChangesAsync();
        var cash = new PaymentTransaction { InvoiceId = invoice.Id, Amount = 40000, AmountTendered = 50000, ChangeAmount = 10000,
            PaymentMethod = "Cash", CashierId = actor, PaidAt = now.AddMinutes(-2), RequestId = Guid.NewGuid() };
        var bank = new PaymentTransaction { InvoiceId = invoice.Id, Amount = 30000, BankReceivedAmount = 30000, PaymentMethod = "BankTransfer",
            CashierId = actor, PaidAt = now.AddMinutes(-1), RequestId = Guid.NewGuid(), Source = "Simulation",
            TransactionReference = "SIM" + Guid.NewGuid().ToString("N")[..8], Note = "Synthetic private cashier review" };
        db.PaymentTransactions.AddRange(cash, bank); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var audits = await db.AuditLogs.CountAsync(); var transactions = await db.PaymentTransactions.CountAsync(); var invoiceCount = await db.Invoices.CountAsync();
        string List(int id, string rest = "") => "/api/portal/invoices?patientId=" + id + rest;
        string Payments(int id, int patientId, string rest = "") => "/api/portal/invoices/" + id + "/payments?patientId=" + patientId + rest;
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(List(a.PatientId))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Portal list anonymous401");
        using (var r = await client.GetAsync(Payments(invoice.Id, a.PatientId))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Portal payments anonymous401");
        foreach (var denied in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticateActor(denied, owner.UserId);
            using var list = await client.GetAsync(List(a.PatientId)); using var payments = await client.GetAsync(Payments(invoice.Id, a.PatientId));
            check(list.StatusCode == HttpStatusCode.Forbidden && payments.StatusCode == HttpStatusCode.Forbidden, "Portal role403 " + denied);
        }
        authenticateActor(RoleCodes.Patient, owner.UserId);
        foreach (var raw in new[] { "", "0", "-1", "abc", "1.0", "2147483648", foreign.PatientId.ToString(), int.MaxValue.ToString() })
        {
            using var list = await client.GetAsync("/api/portal/invoices?patientId=" + raw);
            using var payments = await client.GetAsync("/api/portal/invoices/" + invoice.Id + "/payments?patientId=" + raw);
            check(list.StatusCode == HttpStatusCode.Forbidden && payments.StatusCode == HttpStatusCode.Forbidden, "Portal invalid/foreign explicit profile403");
        }
        using (var r = await client.GetAsync("/api/patients/mine"))
        { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(json.GetArrayLength() == 2 && json.EnumerateArray().All(p => p.GetProperty("patientId").GetInt32() == a.PatientId || p.GetProperty("patientId").GetInt32() == b.PatientId), "Portal active family picker excludes foreign/inactive"); }
        using (var r = await client.GetAsync(List(a.PatientId)))
        {
            var page = (await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>())!;
            check(r.StatusCode == HttpStatusCode.OK && page.TotalCount == 2 && page.Items.All(i => i.PatientCode == a.PatientCode), "Portal profile predicate before count");
            check(page.Items.Select(i => i.InvoiceId).SequenceEqual(new[] { invoice.Id, cancelled.Id }) && page.Items[1].Status == 4, "Portal preserves cancelled invoice and order");
            check(r.Headers.CacheControl?.NoStore == true && page.Items[0].PaidAmount == 70000 && page.Items[0].RemainingAmount == 30000, "Portal no-store and exact debt");
        }
        using (var r = await client.GetAsync(List(b.PatientId))) { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.TotalCount == 1 && page.Items.Single().InvoiceId == family.Id, "Portal family member selected independently"); }
        using (var r = await client.GetAsync(List(inactive.PatientId))) check(r.StatusCode == HttpStatusCode.OK, "Portal ownership still permits archived history API");
        using (var r = await client.GetAsync(List(a.PatientId, "&page=2&pageSize=1&userId=" + other.UserId + "&patientCode=" + foreign.PatientCode)))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.TotalCount == 2 && page.Items.Single().InvoiceId == cancelled.Id, "Portal query spoof cannot override JWT/profile scope or count"); }
        foreach (var query in new[] { "&page=0", "&page=1000001", "&pageSize=0", "&pageSize=51" })
        {
            using var list = await client.GetAsync(List(a.PatientId, query)); using var payments = await client.GetAsync(Payments(invoice.Id, a.PatientId, query));
            check(list.StatusCode == HttpStatusCode.BadRequest && payments.StatusCode == HttpStatusCode.BadRequest, "Portal bounded page400");
        }
        foreach (var pair in new[] { (outsider.Id, a.PatientId), (invoice.Id, b.PatientId), (int.MaxValue, a.PatientId) })
        { using var r = await client.GetAsync(Payments(pair.Item1, pair.Item2)); check(r.StatusCode == HttpStatusCode.Forbidden, "Portal wrong/foreign/missing invoice403 same response"); }
        using (var r = await client.GetAsync(Payments(0, a.PatientId))) check(r.StatusCode == HttpStatusCode.BadRequest, "Portal invalid invoice400");
        using (var r = await client.GetAsync(Payments(invoice.Id, a.PatientId, "&pageSize=1")))
        {
            var page = (await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalPaymentResponse>>())!;
            check(page.TotalCount == 2 && page.TotalPages == 2 && page.Items.Single().PaymentId == bank.Id && page.Items.Single().IsSimulation, "Portal payments scoped order/count/simulation");
            check(r.Headers.CacheControl?.NoStore == true && page.Items.Single().Amount == 30000, "Portal payments no-store actual credited amount");
        }
        using (var r = await client.GetAsync(Payments(invoice.Id, a.PatientId, "&page=2&pageSize=1")))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalPaymentResponse>>(); check(page?.Items.Single().PaymentId == cash.Id && !page.Items.Single().IsSimulation, "Portal second cash transaction page"); }
        using (var r = await client.GetAsync(Payments(family.Id, b.PatientId)))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalPaymentResponse>>(); check(page?.TotalCount == 0 && page.Items.Count == 0, "Portal unpaid invoice empty history200"); }
        using (var r = await client.GetAsync(Payments(invoice.Id, a.PatientId)))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>(); var row = json.GetProperty("items")[0];
            check(row.EnumerateObject().Count() == 6 && !row.TryGetProperty("cashierId", out _) && !row.TryGetProperty("requestId", out _) &&
                !row.TryGetProperty("transactionReference", out _) && !row.TryGetProperty("note", out _), "Portal payment DTO excludes internal/contact fields");
        }
        using (var r = await client.GetAsync(List(a.PatientId)))
        { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); var row = json.GetProperty("items")[0]; check(row.EnumerateObject().Count() == 9 && !row.TryGetProperty("phone", out _), "Portal invoice DTO minimal"); }
        foreach (var endpoint in new[] { "/api/invoices", "/api/invoices/lookup?code=" + invoice.InvoiceCode, "/api/invoices/" + invoice.Id + "/payments", "/api/invoices/" + invoice.Id + "/payments/" + cash.Id + "/receipt" })
        { using var r = await client.GetAsync(endpoint); check(r.StatusCode == HttpStatusCode.Forbidden, "Portal patient still denied cashier endpoint"); }
        using (var r = await client.PostAsJsonAsync("/api/invoices/" + invoice.Id + "/payments", new CashPaymentRequest(10000, 10000, Guid.NewGuid(), "Cash"))) check(r.StatusCode == HttpStatusCode.Forbidden, "Portal patient cannot receive cash");
        authenticateActor(RoleCodes.Patient, other.UserId);
        using (var r = await client.GetAsync(List(a.PatientId))) check(r.StatusCode == HttpStatusCode.Forbidden, "Portal other account cannot use shared phone as ownership");
        using (var r = await client.GetAsync(List(foreign.PatientId))) { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.TotalCount == 1 && page.Items.Single().InvoiceId == outsider.Id, "Portal other account own list valid"); }
        var repo = new InvoicePortalRepository(db);
        check(await repo.OwnsPatientAsync(a.PatientId, owner.UserId), "Portal ownership initial check true");
        await db.Patients.Where(p => p.PatientId == a.PatientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, (int?)other.UserId));
        var staleList = await repo.GetInvoicesAsync(a.PatientId, owner.UserId, 1, 20);
        check(staleList.TotalCount == 0 && staleList.Items.Count == 0, "Portal SQL rechecks ownership after earlier check");
        check(await repo.GetPaymentsAsync(invoice.Id, a.PatientId, owner.UserId, 1, 20) is null, "Portal payment SQL rejects changed ownership");
        await db.Patients.Where(p => p.PatientId == a.PatientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, (int?)owner.UserId));
        check(await db.AuditLogs.CountAsync() == audits && await db.PaymentTransactions.CountAsync() == transactions &&
            await db.Invoices.CountAsync() == invoiceCount, "Portal reads never mutate financial records or audit");
        authenticate(RoleCodes.Admin);
    }
}
