using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
internal static class InvoiceDateFilterHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actor,
        Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Date filter fixtures require owned isolated database.");
        var patient = new Patient { FullName = "Synthetic date filter", Phone = "0900000555", DateOfBirth = new DateOnly(2000, 1, 1) };
        var other = new Patient { FullName = "Synthetic other date filter", Phone = "0900000666", DateOfBirth = new DateOnly(2000, 1, 1) };
        db.Patients.AddRange(patient, other); await db.SaveChangesAsync();
        var start = new DateTime(2026, 10, 13, 17, 0, 0, DateTimeKind.Utc); var until = start.AddDays(1);
        var moments = new[] { start.AddTicks(-10), start, start.AddHours(8), until.AddTicks(-10), until, until.AddHours(1) };
        var prefix = "DATE-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Invoice Make(int i, int patientId) => new() { InvoiceCode = prefix + "-" + i, CreatedByUserId = actor, CreatedAt = moments[i],
            Status = i == 2 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.PendingPayment, PaidAmount = i == 2 ? 20000 : 0, TotalAmount = 100000,
            Visit = new Visit { PatientId = patientId, CreatedByUserId = actor, Status = VisitStatuses.Completed, IsLocked = true } };
        var invoices = Enumerable.Range(0, 6).Select(i => Make(i, patient.PatientId)).ToList();
        var foreign = Make(2, other.PatientId); foreign.InvoiceCode = prefix + "-OTHER";
        db.Invoices.AddRange(invoices); db.Invoices.Add(foreign); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var audits = await db.AuditLogs.CountAsync(); var charges = await db.PaymentTransactions.CountAsync(); var totalInvoices = await db.Invoices.CountAsync();
        string Path(string rest = "") => "/api/invoices?patientCode=" + patient.PatientCode + rest;
        const string sameDay = "&fromDate=2026-10-14&toDate=2026-10-14";
        async Task<PagedResult<InvoiceLookupResponse>> Page(string rest)
        {
            using var r = await client.GetAsync(Path(rest));
            check(r.StatusCode == HttpStatusCode.OK, "Date filter request200");
            return (await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>())!;
        }
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(Path(sameDay))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Date filter anonymous401");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        { authenticate(role); using var r = await client.GetAsync(Path(sameDay)); check(r.StatusCode == HttpStatusCode.Forbidden, "Date filter role403 " + role); }
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist })
        {
            authenticate(role); using var r = await client.GetAsync(Path(sameDay));
            var b = (await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>())!;
            check(r.StatusCode == HttpStatusCode.OK && b.TotalCount == 3 &&
                b.Items.Select(i => i.InvoiceId).SequenceEqual(new[] { invoices[3].Id, invoices[2].Id, invoices[1].Id }), "Date filter includes midnight and last microsecond " + role);
            check(r.Headers.CacheControl?.NoStore == true && b.Items.All(i => i.PatientCode == patient.PatientCode), "Date filter no-store/exact patient " + role);
        }
        var all = await Page(""); check(all.TotalCount == 6, "Date filter omitted keeps all existing scope");
        var from = await Page("&fromDate=2026-10-14"); check(from.TotalCount == 5 && !from.Items.Any(i => i.InvoiceId == invoices[0].Id), "Date filter lower bound inclusive");
        var to = await Page("&toDate=2026-10-14"); check(to.TotalCount == 4 && !to.Items.Any(i => i.InvoiceId == invoices[4].Id), "Date filter upper next-day bound exclusive");
        var next = await Page("&fromDate=2026-10-15&toDate=2026-10-15"); check(next.TotalCount == 2 && next.Items.Any(i => i.InvoiceId == invoices[4].Id), "Date filter next day includes exact boundary");
        var intersect = await Page(sameDay + "&status=2"); check(intersect.TotalCount == 1 && intersect.Items.Single().InvoiceId == invoices[2].Id && intersect.Items.Single().RemainingAmount == 80000, "Date filter status intersect debt");
        var second = await Page(sameDay + "&page=2&pageSize=1"); check(second.TotalCount == 3 && second.TotalPages == 3 && second.Items.Single().InvoiceId == invoices[2].Id, "Date filter before count and pagination");
        var empty = await Page("&fromDate=2026-10-16&toDate=2026-10-16"); check(empty.TotalCount == 0 && empty.Items.Count == 0, "Date filter empty honest page");
        var broad = await Page("&fromDate=1970-01-01&toDate=2099-12-31"); check(broad.TotalCount == 6, "Date filter paginated history no report-only range cap");
        foreach (var rest in new[] { "&fromDate=2026-10-15&toDate=2026-10-14", "&fromDate=0001-01-01", "&toDate=9999-12-31",
            "&fromDate=2026-02-30", "&toDate=not-a-date", "&fromDate=2026-13-01" })
        { using var r = await client.GetAsync(Path(rest)); check(r.StatusCode == HttpStatusCode.BadRequest, "Date filter invalid400 without500"); }
        using (var r = await client.GetAsync("/api/invoices/lookup?code=" + intersect.Items.Single().InvoiceCode))
        { var b = await r.Content.ReadFromJsonAsync<InvoiceLookupResponse>(); check(b?.InvoiceId == invoices[2].Id && b.PaidAmount == 20000, "Date filter selected row exact lookup"); }
        check(await db.AuditLogs.CountAsync() == audits && await db.PaymentTransactions.CountAsync() == charges &&
            await db.Invoices.CountAsync() == totalInvoices, "Date filter read-only no writes");
    }
}
