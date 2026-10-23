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
internal static class InvoiceHistoryHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actor,
        Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Invoice history requires owned isolated database.");
        var patient = new Patient { FullName = "Synthetic invoice history", Phone = "0900000777", DateOfBirth = new DateOnly(2000, 1, 1) };
        var other = new Patient { FullName = "Synthetic other history", Phone = "0900000888", DateOfBirth = new DateOnly(2000, 1, 1) };
        db.Patients.AddRange(patient, other); await db.SaveChangesAsync();
        var prefix = "HISTORY-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var now = DateTime.UtcNow;
        Invoice Make(int index, Patient target) => new() { InvoiceCode = prefix + "-" + index, CreatedByUserId = actor,
            Status = (InvoiceStatus)index, TotalAmount = 100000, PaidAmount = index == 3 ? 100000 : index == 2 ? 30000 : 0,
            CreatedAt = now, Visit = new Visit { PatientId = target.PatientId, CreatedByUserId = actor, Status = VisitStatuses.Completed, IsLocked = true } };
        var invoices = Enumerable.Range(0, 5).Select(i => Make(i, patient)).ToList();
        var otherInvoice = Make(1, other); otherInvoice.InvoiceCode = prefix + "-OTHER";
        db.Invoices.AddRange(invoices); db.Invoices.Add(otherInvoice); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var audits = await db.AuditLogs.CountAsync(); var charges = await db.PaymentTransactions.CountAsync(); var invoiceCount = await db.Invoices.CountAsync();
        string Path(string rest = "") => "/api/invoices?patientCode=" + patient.PatientCode + rest;
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(Path())) check(r.StatusCode == HttpStatusCode.Unauthorized, "History anonymous401");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        { authenticate(role); using var r = await client.GetAsync(Path()); check(r.StatusCode == HttpStatusCode.Forbidden, "Invoice history denied " + role); }
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist })
        {
            authenticate(role); using var r = await client.GetAsync(Path());
            var b = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>();
            check(r.StatusCode == HttpStatusCode.OK && b?.TotalCount == 5 && b.Items.Count == 5, "Invoice history role/count " + role);
            check(b!.Items.All(i => i.PatientCode == patient.PatientCode) && !b.Items.Any(i => i.PatientCode == other.PatientCode), "Invoice history exact patient scope " + role);
            check(r.Headers.CacheControl?.NoStore == true, "Invoice history no-store " + role);
        }
        var ids = new List<int>();
        for (var page = 1; page <= 3; page++)
        {
            using var r = await client.GetAsync(Path("&page=" + page + "&pageSize=2"));
            var b = (await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>())!;
            check(b.TotalCount == 5 && b.TotalPages == 3 && b.Page == page && b.Items.Count == (page == 3 ? 1 : 2), "Invoice history bounded page " + page);
            ids.AddRange(b.Items.Select(i => i.InvoiceId));
        }
        check(ids.SequenceEqual(invoices.OrderByDescending(i => i.Id).Select(i => i.Id)), "Invoice history tied date sorted unique id descending");
        foreach (var status in Enumerable.Range(0, 5))
        {
            using var r = await client.GetAsync(Path("&status=" + status));
            var b = (await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>())!;
            check(b.TotalCount == 1 && b.Items.Single().Status == status && b.Items.Single().InvoiceId == invoices[status].Id,
                "Invoice history status intersection " + status);
        }
        using (var r = await client.GetAsync("/api/invoices?patientCode=" + Uri.EscapeDataString("  " + patient.PatientCode.ToLowerInvariant() + "  ")))
        { var b = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(b?.TotalCount == 5, "Invoice history canonical patient query"); }
        using (var r = await client.GetAsync("/api/invoices"))
        { var b = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(b?.PageSize == 20 && b.Items.Count <= 20 && b.TotalCount == invoiceCount, "Invoice history all default bounded"); }
        foreach (var query in new[] { "patientCode=BN1", "patientCode=BN0000001", "patientCode=BN2147483648", "patientCode=BN00000A",
            "status=-1", "status=5", "page=0", "page=1000001", "pageSize=0", "pageSize=51", "status=invalid" })
        { using var r = await client.GetAsync("/api/invoices?" + query); check(r.StatusCode == HttpStatusCode.BadRequest, "Invoice history invalid400"); }
        using (var r = await client.GetAsync("/api/invoices?patientCode=BN2147483647"))
        { var b = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(b?.TotalCount == 0 && b.Items.Count == 0, "Invoice history missing patient empty"); }
        using (var r = await client.GetAsync(Path("&page=1000000&pageSize=50")))
        { var b = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(b?.Items.Count == 0 && b.TotalCount == 5, "Invoice history overflow safe high page"); }
        using (var r = await client.GetAsync(Path("&status=2")))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>(); var row = json.GetProperty("items")[0];
            check(row.EnumerateObject().Count() == 9 && !row.TryGetProperty("phone", out _) && !row.TryGetProperty("visitId", out _), "Invoice history minimal fields");
            var b = json.GetProperty("items")[0].Deserialize<InvoiceLookupResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            using var exact = await client.GetAsync("/api/invoices/lookup?code=" + b.InvoiceCode);
            var detail = (await exact.Content.ReadFromJsonAsync<InvoiceLookupResponse>())!;
            check(detail.InvoiceId == b.InvoiceId && detail.PaidAmount == 30000 && detail.RemainingAmount == 70000, "Invoice history selection exact debt");
        }
        check(await db.AuditLogs.CountAsync() == audits && await db.PaymentTransactions.CountAsync() == charges &&
            await db.Invoices.CountAsync() == invoiceCount, "Invoice history reads never mutate");
        var selected = invoices[1];
        using (var r = await client.PostAsJsonAsync($"/api/invoices/{selected.Id}/payments",
            new CashPaymentRequest(40000, 50000, Guid.NewGuid(), "Cash")))
            check(r.StatusCode == HttpStatusCode.OK, "Invoice history selected invoice can receive cash");
        using (var r = await client.GetAsync($"/api/invoices/{selected.Id}/payments"))
        {
            var history = (await r.Content.ReadFromJsonAsync<List<CashPaymentResponse>>())!;
            check(history.Count == 1 && history[0].InvoiceId == selected.Id && history[0].Amount == 40000, "Invoice history opens exact transaction history");
            using var receipt = await client.GetAsync($"/api/invoices/{selected.Id}/payments/{history[0].Id}/receipt");
            check(receipt.StatusCode == HttpStatusCode.OK && receipt.Headers.CacheControl?.NoStore == true, "Invoice history opens exact receipt no-store");
        }
        using (var r = await client.GetAsync(Path("&status=2")))
        {
            var b = (await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>())!;
            var updated = b.Items.Single(i => i.InvoiceId == selected.Id);
            check(b.TotalCount == 2 && updated.PaidAmount == 40000 && updated.RemainingAmount == 60000,
                "Invoice history reread reflects paid amount and updated state");
        }

    }
}
