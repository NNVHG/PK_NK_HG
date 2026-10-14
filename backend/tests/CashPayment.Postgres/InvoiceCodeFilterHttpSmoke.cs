using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
internal static class InvoiceCodeFilterHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actor, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Code filter fixtures require owned isolated database.");
        var patient = new Patient { FullName = "Synthetic code filter family", Phone = "03" + Random.Shared.Next(10000000, 99999999), DateOfBirth = new(2000, 1, 1) };
        var other = new Patient { FullName = "Synthetic other code filter family", Phone = patient.Phone, DateOfBirth = new(2000, 1, 1) };
        db.Patients.AddRange(patient, other); await db.SaveChangesAsync();
        var prefix = "CODE-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var time = new DateTime(2026, 10, 15, 1, 0, 0, DateTimeKind.Utc);
        Invoice Make(string code, Patient p) => new() { InvoiceCode = code, CreatedAt = time, CreatedByUserId = actor,
            TotalAmount = 100000, PaidAmount = 40000, Status = InvoiceStatus.PartiallyPaid,
            Visit = new Visit { PatientId = p.PatientId, CreatedByUserId = actor, Status = VisitStatuses.Completed, IsLocked = true } };
        var target = Make(prefix + "-X", patient); var near = Make(prefix + "-X-OTHER", patient); var foreign = Make(prefix + "-OTHER", other);
        db.Invoices.AddRange(target, near, foreign); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var count = await db.Invoices.CountAsync(); var payments = await db.PaymentTransactions.CountAsync(); var audits = await db.AuditLogs.CountAsync();
        string Url(string? code, string suffix = "") => "/api/invoices?invoiceCode=" + Uri.EscapeDataString(code ?? "") + suffix;
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(Url(target.InvoiceCode))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Invoice code filter anonymous401");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant }) {
            authenticate(role); using var r = await client.GetAsync(Url(target.InvoiceCode)); check(r.StatusCode == HttpStatusCode.Forbidden, "Invoice code filter role403 " + role);
        }
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist }) {
            authenticate(role); using var r = await client.GetAsync(Url(" " + target.InvoiceCode.ToLowerInvariant() + " "));
            var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>();
            check(r.StatusCode == HttpStatusCode.OK && page?.TotalCount == 1 && page.Items.Single().InvoiceId == target.Id, "Invoice code canonical exact before count " + role);
            check(r.Headers.CacheControl?.NoStore == true && page!.Items.Single().RemainingAmount == 60000, "Invoice code no-store debt " + role);
        }
        using (var r = await client.GetAsync(Url(target.InvoiceCode, "&patientCode=" + patient.PatientCode + "&keyword=Synthetic&status=2&fromDate=2026-10-15&toDate=2026-10-15&pageSize=1"))) {
            var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.TotalCount == 1 && page.Items.Single().InvoiceId == target.Id, "Invoice code AND name patient status Vietnam dates");
        }
        foreach (var suffix in new[] { "&patientCode=" + other.PatientCode, "&status=3", "&keyword=NO-MATCH", "&fromDate=2026-10-16", "&toDate=2026-10-14" }) {
            using var r = await client.GetAsync(Url(target.InvoiceCode, suffix)); var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>();
            check(r.StatusCode == HttpStatusCode.OK && page?.TotalCount == 0 && page.Items.Count == 0, "Invoice code conflicting filter returns honest empty200");
        }
        using (var r = await client.GetAsync(Url(target.InvoiceCode, "&page=2&pageSize=1"))) {
            var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.TotalCount == 1 && page.Items.Count == 0, "Invoice code count before empty second page");
        }
        using (var r = await client.GetAsync(Url(prefix))) {
            var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(r.StatusCode == HttpStatusCode.OK && page?.TotalCount == 0, "Invoice code never broadens to prefix matching");
        }
        using (var r = await client.GetAsync(Url(near.InvoiceCode))) {
            var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.Items.Single().InvoiceId == near.Id, "Invoice code longer near match selected only by exact code");
        }
        foreach (var blank in new[] { "", "  " }) {
            using var r = await client.GetAsync(Url(blank, "&patientCode=" + patient.PatientCode)); var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>();
            check(page?.TotalCount == 2, "Invoice code blank preserves patient scope");
        }
        foreach (var invalid in new[] { "INV X", "INV%", "INV_", "INV'", "HÓA-01", "ınv-01", new string('A', 51) }) {
            using var r = await client.GetAsync(Url(invalid)); check(r.StatusCode == HttpStatusCode.BadRequest, "Invoice code invalid400 no fallback");
        }
        using (var r = await client.GetAsync(Url(new string('A', 50)))) check(r.StatusCode == HttpStatusCode.OK, "Invoice code canonical50 accepted");
        using (var r = await client.GetAsync("/api/invoices/lookup?code=" + target.InvoiceCode)) {
            var invoice = await r.Content.ReadFromJsonAsync<InvoiceLookupResponse>(); check(r.StatusCode == HttpStatusCode.OK && invoice?.InvoiceId == target.Id, "Existing exact lookup remains available");
        }
        check(await db.Invoices.CountAsync() == count && await db.PaymentTransactions.CountAsync() == payments && await db.AuditLogs.CountAsync() == audits,
            "Invoice code read never changes financial records or audit");
        authenticate(RoleCodes.Admin);
    }
}
