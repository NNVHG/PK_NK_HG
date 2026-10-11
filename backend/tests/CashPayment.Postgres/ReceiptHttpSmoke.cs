using System.Net;
using System.Net.Http.Json;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

internal static class ReceiptHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int invoiceId,
        int paymentId, Action<string> authenticate, Action<bool, string> check)
    {
        var root = $"/api/invoices/{invoiceId}/payments/{paymentId}";
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync(root + "/receipt")) check(response.StatusCode == HttpStatusCode.Unauthorized, "Receipt anonymous 401");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role);
            using var preview = await client.GetAsync(root + "/receipt");
            using var print = await client.PostAsync(root + "/reprint", null);
            check(preview.StatusCode == HttpStatusCode.Forbidden && print.StatusCode == HttpStatusCode.Forbidden, $"Receipt {role} read/write 403");
        }
        authenticate(RoleCodes.Receptionist);
        using (var response = await client.GetAsync($"/api/invoices/2147483647/payments/{paymentId}/receipt")) check(response.StatusCode == HttpStatusCode.NotFound, "Receipt mismatched invoice/payment 404");
        using (var response = await client.PostAsync($"/api/invoices/{invoiceId}/payments/0/reprint", null)) check(response.StatusCode == HttpStatusCode.BadRequest, "Receipt invalid route ID 400");
        await using var db = new DentalDbContext(options);
        var original = await db.PaymentTransactions.AsNoTracking().SingleAsync(p => p.Id == paymentId);
        using (var response = await client.GetAsync(root + "/receipt"))
        {
            var dto = await response.Content.ReadFromJsonAsync<ReceiptPrintDto>();
            check(response.StatusCode == HttpStatusCode.OK && dto?.PrintCount == 0 && dto.PrintNumber == 1 && dto.PrintedAt is null, "Receipt preview does not count as print");
            check(response.Headers.CacheControl?.NoStore == true, "Receipt no-store protects patient information");
            check(dto?.PatientName is not null && dto.PatientCode.StartsWith("BN") && !string.IsNullOrWhiteSpace(dto.CashierName), "Receipt actual patient/cashier identity");
            check(dto?.Amount == 40000 && dto.AmountTendered == 50000 && dto.ChangeAmount == 10000 && dto.PaidAmount == 100000 && dto.RemainingAmount == 0, "Receipt current transaction and current debt summary");
            check(dto?.Items.Count == 1 && dto.Items[0].Quantity == 2 && dto.Items[0].UnitPrice == 50000 && dto.Items[0].TotalAmount == 100000,
                "Receipt item quantity and price remain invoice snapshots");
        }
        using (var response = await client.PostAsync(root + "/reprint", null))
        {
            var dto = await response.Content.ReadFromJsonAsync<ReceiptPrintDto>();
            check(response.StatusCode == HttpStatusCode.OK && dto?.PrintNumber == 1 && dto.PrintCount == 1 && dto.PrintedAt is not null, "Receipt first request ordinal 1");
        }
        authenticate(RoleCodes.Admin);
        using (var response = await client.PostAsync(root + "/reprint", null))
        {
            var dto = await response.Content.ReadFromJsonAsync<ReceiptPrintDto>();
            check(response.StatusCode == HttpStatusCode.OK && dto?.PrintNumber == 2 && dto.PrintCount == 2 && dto.PrintedAt is not null, "Receipt Admin reprint ordinal 2");
        }
        var stored = await db.PaymentTransactions.AsNoTracking().SingleAsync(p => p.Id == paymentId);
        check(stored.PrintCount == 2 && stored.Amount == original.Amount && stored.AmountTendered == original.AmountTendered &&
            stored.ChangeAmount == original.ChangeAmount && stored.RequestId == original.RequestId && stored.CashierId == original.CashierId,
            "Receipt metadata never alters financial transaction");
        check(await db.AuditLogs.CountAsync(a => a.EntityType == "PaymentTransaction" && a.EntityId == paymentId &&
            (a.Action == "PrintReceipt" || a.Action == "ReprintReceipt")) == 2, "Receipt increment and actor audit persisted atomically");
        var calls = await Task.WhenAll(client.PostAsync(root + "/reprint", null), client.PostAsync(root + "/reprint", null));
        try
        {
            check(calls.All(r => r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict) && calls.Any(r => r.IsSuccessStatusCode), "Receipt concurrent print no 500 or lost increment");
            var successes = calls.Count(r => r.IsSuccessStatusCode);
            stored = await db.PaymentTransactions.AsNoTracking().SingleAsync(p => p.Id == paymentId);
            var ordinals = new List<int>();
            foreach (var response in calls.Where(r => r.IsSuccessStatusCode)) ordinals.Add((await response.Content.ReadFromJsonAsync<ReceiptPrintDto>())!.PrintNumber);
            check(stored.PrintCount == 2 + successes && ordinals.Distinct().Count() == successes &&
                await db.AuditLogs.CountAsync(a => a.EntityType == "PaymentTransaction" && a.EntityId == paymentId &&
                    (a.Action == "PrintReceipt" || a.Action == "ReprintReceipt")) == stored.PrintCount,
                "Receipt concurrent committed ordinals have matching audit count");
        }
        finally { foreach (var response in calls) response.Dispose(); }
        await using var failing = new DentalDbContext(options);
        var beforeFailure = await db.PaymentTransactions.AsNoTracking().SingleAsync(p => p.Id == paymentId);
        try
        {
            // Test-only invalid FK forces audit insertion to fail after metadata was prepared.
            await new ReceiptRepository(failing).PrintAsync(new(invoiceId, paymentId), int.MaxValue, null);
            throw new InvalidOperationException("Test expected an audit FK failure.");
        }
        catch (DbUpdateException) { }
        var afterFailure = await db.PaymentTransactions.AsNoTracking().SingleAsync(p => p.Id == paymentId);
        check(afterFailure.PrintCount == beforeFailure.PrintCount, "Receipt audit failure rolls back print metadata increment");
    }
}
