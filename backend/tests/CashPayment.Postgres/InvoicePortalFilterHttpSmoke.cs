using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

internal static class InvoicePortalFilterHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actor,
        int ownerId, int otherId, int emptyOwnerId, Action<string, int> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Portal filter fixtures require owned isolated database.");
        Patient Profile(int userId) => new() { UserId = userId, FullName = "Synthetic filter patient",
            Phone = "03" + Random.Shared.Next(10000000, 99999999), DateOfBirth = new(2000, 1, 1), IsActive = true };
        var own = Profile(ownerId); var foreign = Profile(otherId);
        db.Patients.AddRange(own, foreign); await db.SaveChangesAsync();
        var start = new DateTime(2026, 10, 13, 17, 0, 0, DateTimeKind.Utc);
        var until = start.AddDays(1);
        var prefix = "PF-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Invoice Make(string code, Patient patient, DateTime date, InvoiceStatus status) => new() {
            InvoiceCode = prefix + code, CreatedAt = date, CreatedByUserId = actor, TotalAmount = 100000,
            PaidAmount = status == InvoiceStatus.Paid ? 100000 : status == InvoiceStatus.PartiallyPaid ? 40000 : 0,
            Status = status, Visit = new Visit { PatientId = patient.PatientId, CreatedByUserId = actor, Status = VisitStatuses.Completed, IsLocked = true } };
        var before = Make("-BEFORE", own, start.AddTicks(-10), InvoiceStatus.PartiallyPaid);
        var first = Make("-START", own, start, InvoiceStatus.PartiallyPaid);
        var last = Make("-END", own, until.AddTicks(-10), InvoiceStatus.Paid);
        var next = Make("-NEXT", own, until, InvoiceStatus.PartiallyPaid);
        var cancelled = Make("-CANCEL", own, start.AddHours(1), InvoiceStatus.Cancelled);
        var draft = Make("-DRAFT", own, start.AddHours(2), InvoiceStatus.Draft);
        var same = Make("-SAME", own, start, InvoiceStatus.PartiallyPaid);
        var foreignInvoice = Make("-FOREIGN", foreign, start, InvoiceStatus.PartiallyPaid);
        db.Invoices.AddRange(before, first, last, next, cancelled, draft, same, foreignInvoice);
        await db.SaveChangesAsync();
        db.PaymentTransactions.Add(new PaymentTransaction { InvoiceId = before.Id, Amount = 40000, AmountTendered = 40000,
            PaymentMethod = "Cash", CashierId = actor, PaidAt = start.AddHours(3), RequestId = Guid.NewGuid() });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var invoiceCount = await db.Invoices.CountAsync(); var payments = await db.PaymentTransactions.CountAsync(); var audits = await db.AuditLogs.CountAsync();
        string Url(string suffix = "", int? patient = null) => "/api/portal/invoices?patientId=" + (patient ?? own.PatientId) + suffix;
        const string day = "&fromDate=2026-10-14&toDate=2026-10-14";
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(Url(day))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Portal filtered list anonymous401");
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant }) {
            authenticate(role, ownerId); using var r = await client.GetAsync(Url(day)); check(r.StatusCode == HttpStatusCode.Forbidden, "Portal filtered list staff403 " + role);
        }
        authenticate(RoleCodes.Patient, ownerId);
        using (var r = await client.GetAsync(Url(day))) {
            var page = (await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>())!;
            check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true, "Portal filtered list200 no-store");
            check(page.TotalCount == 5 && page.Items.Select(i => i.InvoiceId).SequenceEqual(new[] { last.Id, draft.Id, cancelled.Id, same.Id, first.Id }), "Portal Vietnam date inclusive start/end microsecond exclusive next midnight stable order");
            check(page.Items.All(i => i.PatientCode == own.PatientCode) && page.Items.All(i => i.InvoiceId != before.Id), "Portal filter uses CreatedAt not PaidAt and keeps owner/profile scope");
        }
        using (var r = await client.GetAsync(Url(day + "&status=2&pageSize=1&userId=" + otherId))) {
            var page = (await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>())!;
            check(page.TotalCount == 2 && page.TotalPages == 2 && page.Items.Single().InvoiceId == same.Id, "Portal status/date AND ownership before count and page");
        }
        using (var r = await client.GetAsync(Url(day + "&status=2&pageSize=1&page=2"))) {
            var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.Items.Single().InvoiceId == first.Id && page.TotalCount == 2, "Portal applied status/date second page");
        }
        using (var r = await client.GetAsync(Url(day + "&status=0"))) {
            var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.TotalCount == 1 && page.Items.Single().InvoiceId == draft.Id, "Portal zero Draft status is not omitted");
        }
        using (var r = await client.GetAsync(Url(day + "&status=4"))) {
            var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.Items.Single().InvoiceId == cancelled.Id, "Portal cancelled filter remains readable");
        }
        using (var r = await client.GetAsync(Url("&fromDate=2026-10-14"))) {
            var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.TotalCount == 6 && page.Items.Any(i => i.InvoiceId == next.Id), "Portal open-ended From");
        }
        using (var r = await client.GetAsync(Url("&toDate=2026-10-14"))) {
            var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.TotalCount == 6 && page.Items.Any(i => i.InvoiceId == before.Id), "Portal open-ended To");
        }
        using (var r = await client.GetAsync(Url())) {
            var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.TotalCount == 7, "Portal clearing filters includes all scoped history");
        }
        using (var r = await client.GetAsync(Url("&toDate=1900-01-01&status=3"))) {
            var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(r.StatusCode == HttpStatusCode.OK && page?.TotalCount == 0 && page.Items.Count == 0, "Portal valid empty filter200");
        }
        foreach (var invalid in new[] { "&status=-1", "&status=5", "&status=abc", "&fromDate=bad", "&fromDate=2026-02-29", "&fromDate=0001-01-01",
            "&toDate=9999-12-31", "&fromDate=2026-10-15&toDate=2026-10-14", "&page=0" }) {
            using var r = await client.GetAsync(Url(invalid)); check(r.StatusCode == HttpStatusCode.BadRequest, "Portal invalid filter400");
        }
        foreach (var raw in new[] { "", "0", foreign.PatientId.ToString() }) {
            using var r = await client.GetAsync("/api/portal/invoices?patientId=" + raw + day + "&status=2");
            check(r.StatusCode == HttpStatusCode.Forbidden, "Portal filters never bypass required owned profile403");
        }
        using (var r = await client.GetAsync(Url(day + "&status=2", foreign.PatientId)))
            check(r.StatusCode == HttpStatusCode.Forbidden, "Portal filtered list rejects another account profile");
        await db.Patients.Where(p => p.PatientId == own.PatientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, (int?)emptyOwnerId));
        var rows = await new InvoicePortalRepository(db).GetInvoicesAsync(own.PatientId, ownerId, 1, 20, 2, start, until);
        check(rows.TotalCount == 0 && rows.Items.Count == 0, "Portal filtered SQL repeats ownership after prior scope check");
        await db.Patients.Where(p => p.PatientId == own.PatientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, (int?)ownerId));
        check(await db.Invoices.CountAsync() == invoiceCount && await db.PaymentTransactions.CountAsync() == payments && await db.AuditLogs.CountAsync() == audits,
            "Portal filter reads leave invoices payments audit unchanged");
    }
}
