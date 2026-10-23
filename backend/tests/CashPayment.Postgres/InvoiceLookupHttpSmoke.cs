using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
internal static class InvoiceLookupHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actor,
        int patientId, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Lookup fixtures require owned isolated database.");
        var prefix = "LOOKUP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Invoice Make(string suffix, InvoiceStatus status) => new() { InvoiceCode = prefix + suffix, Status = status,
            TotalAmount = 100000, CreatedAt = DateTime.UtcNow.AddDays(-30), CreatedByUserId = actor,
            Visit = new Visit { PatientId = patientId, CreatedByUserId = actor, Status = VisitStatuses.Completed, IsLocked = true } };
        var invoice = Make("-A", InvoiceStatus.PendingPayment); var cancelled = Make("-OLD", InvoiceStatus.Cancelled);
        var draft = Make("-DRAFT", InvoiceStatus.Draft); var paid = Make("-PAID", InvoiceStatus.Paid); paid.PaidAmount = paid.TotalAmount;
        db.Invoices.AddRange(invoice, cancelled, draft, paid); await db.SaveChangesAsync();
        var replacement = Make("-NEW", InvoiceStatus.PendingPayment); replacement.Visit = cancelled.Visit; replacement.VisitId = cancelled.VisitId;
        db.Invoices.Add(replacement); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var audits = await db.AuditLogs.CountAsync(); var payments = await db.PaymentTransactions.CountAsync();
        string Path(string code) => "/api/invoices/lookup?code=" + Uri.EscapeDataString(code);
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(Path(invoice.InvoiceCode))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Lookup anonymous401");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        { authenticate(role); using var r = await client.GetAsync(Path(invoice.InvoiceCode)); check(r.StatusCode == HttpStatusCode.Forbidden, "Lookup denied " + role); }
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist })
        {
            authenticate(role); using var r = await client.GetAsync(Path("  " + invoice.InvoiceCode.ToLowerInvariant() + "  "));
            var body = await r.Content.ReadFromJsonAsync<InvoiceLookupResponse>();
            check(r.StatusCode == HttpStatusCode.OK && body?.InvoiceId == invoice.Id && body.RemainingAmount == 100000, "Lookup canonical exact " + role);
            check(r.Headers.CacheControl?.NoStore == true, "Lookup no-store " + role);
            check(body is not null && body.PatientCode.StartsWith("BN") && !string.IsNullOrEmpty(body.PatientName), "Lookup patient comparison " + role);
        }
        foreach (var code in new[] { "", "INV_1", "INV 1", new string('A', 51), "HÓA-1" })
        { using var r = await client.GetAsync(Path(code)); check(r.StatusCode == HttpStatusCode.BadRequest, "Lookup invalid400"); }
        foreach (var code in new[] { prefix, "MISSING-1" })
        { using var r = await client.GetAsync(Path(code)); check(r.StatusCode == HttpStatusCode.NotFound, "Lookup exact missing404"); }
        foreach (var sample in new[] { draft, paid })
        {
            using var r = await client.GetAsync(Path(sample.InvoiceCode));
            var b = await r.Content.ReadFromJsonAsync<InvoiceLookupResponse>();
            check(b?.InvoiceId == sample.Id && b.Status == (int)sample.Status && b.RemainingAmount == sample.TotalAmount - sample.PaidAmount, "Lookup draft/paid state");
        }
        using (var r = await client.GetAsync(Path(cancelled.InvoiceCode)))
        { var b = await r.Content.ReadFromJsonAsync<InvoiceLookupResponse>(); check(b?.InvoiceId == cancelled.Id && b.Status == 4, "Lookup old cancelled never replacement"); }
        using (var r = await client.GetAsync(Path(invoice.InvoiceCode)))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            check(json.EnumerateObject().Count() == 9 && !json.TryGetProperty("phone", out _) && !json.TryGetProperty("visitId", out _), "Lookup minimal projection");
        }
        check(await db.AuditLogs.CountAsync() == audits && await db.PaymentTransactions.CountAsync() == payments &&
            await db.Invoices.Where(i => i.Id == invoice.Id).Select(i => i.Status).SingleAsync() == InvoiceStatus.PendingPayment, "Lookup GET read only");
        var request = new CashPaymentRequest(40000, 50000, Guid.NewGuid(), "Cash");
        using (var r = await client.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", request))
        { var b = await r.Content.ReadFromJsonAsync<CashPaymentResponse>(); check(r.StatusCode == HttpStatusCode.OK && b?.InvoiceId == invoice.Id && b.ChangeAmount == 10000, "Lookup exact cash payment");
            using var receipt = await client.GetAsync($"/api/invoices/{invoice.Id}/payments/{b!.Id}/receipt");
            check(receipt.StatusCode == HttpStatusCode.OK, "Lookup paid receipt available");
        }
        using (var r = await client.GetAsync(Path(invoice.InvoiceCode)))
        { var b = await r.Content.ReadFromJsonAsync<InvoiceLookupResponse>(); check(b?.Status == 2 && b.PaidAmount == 40000 && b.RemainingAmount == 60000, "Lookup refreshed partial debt"); }
        using (var r = await client.PostAsJsonAsync($"/api/invoices/{cancelled.Id}/payments", new CashPaymentRequest(10000, 10000, Guid.NewGuid(), "Cash")))
            check(r.StatusCode == HttpStatusCode.Conflict, "Lookup cancelled cannot charge");
        check(await db.Invoices.Where(i => i.Id == replacement.Id).Select(i => i.PaidAmount).SingleAsync() == 0, "Lookup replacement untouched");
    }
}
