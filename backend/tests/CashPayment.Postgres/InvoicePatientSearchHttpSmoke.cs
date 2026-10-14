using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
internal static class InvoicePatientSearchHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actor,
        Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Invoice keyword fixtures require owned isolated database.");
        var prefix = "ISEARCH-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var phone = "09" + Random.Shared.Next(10000000, 99999999);
        Patient MakePatient(string name, string contact) => new() { FullName = name, Phone = contact, DateOfBirth = new DateOnly(2000, 1, 1) };
        var a = MakePatient("Trần " + prefix + " 100%_ O'Neil", phone);
        var b = MakePatient("Gia đình " + prefix, phone);
        var c = MakePatient("TRẦN " + prefix + " 100XA", "08" + phone[2..]);
        db.Patients.AddRange(a, b, c); await db.SaveChangesAsync();
        var start = new DateTime(2026, 10, 13, 17, 0, 0, DateTimeKind.Utc);
        Invoice Make(int index, Patient patient, InvoiceStatus status, DateTime created) => new() {
            InvoiceCode = prefix + "-" + index, CreatedAt = created, CreatedByUserId = actor, Status = status, TotalAmount = 100000,
            PaidAmount = status == InvoiceStatus.Paid ? 100000 : status == InvoiceStatus.PartiallyPaid ? 20000 : 0,
            Visit = new Visit { PatientId = patient.PatientId, CreatedByUserId = actor, Status = VisitStatuses.Completed, IsLocked = true } };
        var ia = Make(1, a, InvoiceStatus.PendingPayment, start.AddHours(1));
        var old = Make(2, a, InvoiceStatus.Paid, start.AddDays(-1));
        var ib = Make(3, b, InvoiceStatus.PartiallyPaid, start.AddHours(3));
        var ic = Make(4, c, InvoiceStatus.PendingPayment, start.AddHours(2));
        db.Invoices.AddRange(ia, old, ib, ic); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var audits = await db.AuditLogs.CountAsync(); var payments = await db.PaymentTransactions.CountAsync(); var count = await db.Invoices.CountAsync();
        string Path(string keyword, string rest = "") => "/api/invoices?keyword=" + Uri.EscapeDataString(keyword) + rest;
        async Task<PagedResult<InvoiceLookupResponse>> Read(string keyword, string rest = "")
        {
            using var r = await client.GetAsync(Path(keyword, rest)); check(r.StatusCode == HttpStatusCode.OK, "Invoice keyword request200");
            return (await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>())!;
        }
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(Path(phone))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Invoice keyword anonymous401");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        { authenticate(role); using var r = await client.GetAsync(Path(phone)); check(r.StatusCode == HttpStatusCode.Forbidden, "Invoice keyword role403 " + role); }
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist })
        {
            authenticate(role); using var r = await client.GetAsync(Path("  trần " + prefix.ToLowerInvariant() + "  "));
            var page = (await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>())!;
            check(r.StatusCode == HttpStatusCode.OK && page.TotalCount == 3 && page.Items.All(i => i.PatientCode == a.PatientCode || i.PatientCode == c.PatientCode),
                "Invoice keyword case/diacritic name " + role);
            check(r.Headers.CacheControl?.NoStore == true, "Invoice keyword no-store " + role);
        }
        var family = await Read(phone); check(family.TotalCount == 3 && family.Items.Select(i => i.PatientCode).Distinct().Count() == 2, "Invoice keyword shared phone keeps distinct family profiles");
        var literal = await Read(prefix + " 100%_"); check(literal.TotalCount == 2 && literal.Items.All(i => i.PatientCode == a.PatientCode), "Invoice keyword SQL wildcard treated literal");
        var quoted = await Read(prefix + " 100%_ O'Neil"); check(quoted.TotalCount == 2, "Invoice keyword quote stays parameter");
        var mismatch = await Read(prefix + " NOT-FOUND"); check(mismatch.TotalCount == 0 && mismatch.Items.Count == 0, "Invoice keyword no result honest");
        var state = await Read(phone, "&status=2"); check(state.TotalCount == 1 && state.Items.Single().InvoiceId == ib.Id, "Invoice keyword state intersection");
        var exact = await Read(phone, "&patientCode=" + b.PatientCode); check(exact.TotalCount == 1 && exact.Items.Single().InvoiceId == ib.Id, "Invoice keyword exact patient intersection");
        var day = await Read(phone, "&fromDate=2026-10-14&toDate=2026-10-14"); check(day.TotalCount == 2 && !day.Items.Any(i => i.InvoiceId == old.Id), "Invoice keyword date intersection");
        var paged = await Read(prefix, "&page=2&pageSize=2");
        check(paged.TotalCount == 4 && paged.TotalPages == 2 && paged.Items.Select(i => i.InvoiceId).SequenceEqual(new[] { ia.Id, old.Id }), "Invoice keyword before count/page/order");
        var all = await Read("   "); check(all.TotalCount == count && all.Items.Count <= 20, "Invoice keyword blank keeps old bounded scope");
        using (var r = await client.GetAsync(Path(new string('A', 100)))) check(r.StatusCode == HttpStatusCode.OK, "Invoice keyword boundary100 accepted");
        using (var r = await client.GetAsync(Path(new string('A', 101)))) check(r.StatusCode == HttpStatusCode.BadRequest, "Invoice keyword101 rejected400");
        using (var r = await client.GetAsync(Path(phone, "&patientCode=BN1"))) check(r.StatusCode == HttpStatusCode.BadRequest, "Invoice keyword invalid other filter never broadened");
        using (var r = await client.GetAsync(Path(phone)))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>(); var row = json.GetProperty("items")[0];
            check(row.EnumerateObject().Count() == 9 && !row.TryGetProperty("phone", out _) && !row.TryGetProperty("visitId", out _), "Invoice keyword keeps minimal response no contact");
        }
        using (var r = await client.GetAsync("/api/invoices/lookup?code=" + state.Items.Single().InvoiceCode))
        { var detail = await r.Content.ReadFromJsonAsync<InvoiceLookupResponse>(); check(detail?.InvoiceId == ib.Id && detail.RemainingAmount == 80000, "Invoice keyword selection exact debt lookup"); }
        check(await db.AuditLogs.CountAsync() == audits && await db.PaymentTransactions.CountAsync() == payments &&
            await db.Invoices.CountAsync() == count, "Invoice keyword GET no mutation or audit");
    }
}
