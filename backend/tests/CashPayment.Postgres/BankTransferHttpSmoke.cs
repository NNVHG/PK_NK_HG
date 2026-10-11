using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class BankTransferHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actor,
        int patientId, string webhookKey, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var invoice = new Invoice { InvoiceCode = "INV-20261011-9001", TotalAmount = 100000,
            Status = InvoiceStatus.PendingPayment, CreatedByUserId = actor, CreatedAt = DateTime.UtcNow,
            Visit = new Visit { PatientId = patientId, CreatedByUserId = actor, Status = VisitStatuses.Completed, IsLocked = true } };
        db.Invoices.Add(invoice); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        const string endpoint = "/api/webhooks/payment/bank-transfer";
        var body = new BankTransferRequest("PKNK " + invoice.InvoiceCode, 30000, "BANK0001");
        async Task<HttpResponseMessage> Post(BankTransferRequest request, bool simulated = false)
        {
            if (simulated) return await client.PostAsJsonAsync(endpoint + "/simulate", new BankTransferSimulationRequest(
                request.AddInfo.Replace("PKNK ", ""), request.Amount, request.TransactionReference));
            var id = (BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(request.TransactionReference))) & long.MaxValue) + 1;
            return await client.PostAsJsonAsync(endpoint, new SePayWebhookRequest(id, "in", request.Amount, request.TransactionReference, request.AddInfo, "12345678"));
        }
        async Task<BankTransferResponse> ProviderReceipt(HttpResponseMessage response)
        {
            var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
            check(envelope.GetProperty("success").GetBoolean(), "Bank: provider success envelope");
            return envelope.GetProperty("payment").Deserialize<BankTransferResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
        authenticate(RoleCodes.Admin);
        using (var response = await Post(body)) check(response.StatusCode == HttpStatusCode.Unauthorized, "Bank: JWT cannot authenticate provider webhook");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Apikey", "invalid");
        using (var response = await Post(body)) check(response.StatusCode == HttpStatusCode.Unauthorized, "Bank: invalid API key 401");
        authenticate(RoleCodes.Patient);
        using (var response = await Post(body, true)) check(response.StatusCode == HttpStatusCode.Forbidden, "Bank: patient simulation 403");
        authenticate(RoleCodes.Receptionist);
        using (var response = await client.GetAsync($"/api/invoices/{invoice.Id}/bank-transfer-info"))
        {
            var info = await response.Content.ReadFromJsonAsync<BankTransferInfo>();
            check(response.StatusCode == HttpStatusCode.OK && info!.QrUrl is null && info.SimulationEnabled, "Bank: unconfigured QR honest and offline simulation enabled");
        }
        // Synthetic banking metadata only in this isolated test database.
        db.SystemConfigs.AddRange(new SystemConfig { Key = "Payment:BankBin", Value = "999999" },
            new SystemConfig { Key = "Payment:AccountNumber", Value = "12345678" }, new SystemConfig { Key = "Payment:AccountName", Value = "Synthetic test account" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        using (var response = await client.GetAsync($"/api/invoices/{invoice.Id}/bank-transfer-info"))
        {
            var info = await response.Content.ReadFromJsonAsync<BankTransferInfo>();
            check(info!.QrUrl!.Contains("amount=100000") && info.QrUrl.Contains("PKNK%20INV-20261011-9001"), "Bank: dynamic QR from stored settings");
        }
        using (var response = await client.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", new CashPaymentRequest(20000, 20000, Guid.NewGuid())))
            check(response.StatusCode == HttpStatusCode.OK, "Bank: cash before bank split payment");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Apikey", webhookKey);
        using (var response = await client.PostAsJsonAsync(endpoint, new SePayWebhookRequest(100, "out", 30000, "BANK0001", body.AddInfo, "12345678")))
            check(response.StatusCode == HttpStatusCode.BadRequest, "Bank: outgoing debit is never credited");
        using (var response = await client.PostAsJsonAsync(endpoint, new SePayWebhookRequest(100, "in", 30000, "BANK0001", body.AddInfo, "wrong-account")))
            check(response.StatusCode == HttpStatusCode.BadRequest, "Bank: wrong receiving account is never credited");
        using (var response = await Post(body with { AddInfo = "wrong content" })) check(response.StatusCode == HttpStatusCode.BadRequest, "Bank: invalid content 400");
        using (var response = await Post(body with { AddInfo = "PKNK INV-20261011-9999" })) check(response.StatusCode == HttpStatusCode.NotFound, "Bank: unknown invoice 404");
        BankTransferResponse receipt;
        using (var response = await Post(body))
        {
            check(response.StatusCode == HttpStatusCode.OK, "Bank: valid provider webhook 200");
            receipt = await ProviderReceipt(response);
            check(receipt.Amount == 30000 && receipt.BankReceivedAmount == 30000 && receipt.Source == "Webhook", "Bank: partial bank receipt");
        }
        using (var response = await Post(body)) check((await ProviderReceipt(response)).Id == receipt.Id, "Bank: replay preserves receipt");
        var originalEventId = (BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(body.TransactionReference))) & long.MaxValue) + 1;
        using (var response = await client.PostAsJsonAsync(endpoint, new SePayWebhookRequest(originalEventId, "in", body.Amount, "DIFFREF1", body.AddInfo, "12345678")))
            check(response.StatusCode == HttpStatusCode.Conflict, "Bank: same provider event cannot credit a changed reference");
        using (var response = await Post(body with { Amount = 30001 })) check(response.StatusCode == HttpStatusCode.Conflict, "Bank: reference reuse mismatch 409");
        authenticate(RoleCodes.Admin);
        using (var response = await Post(body with { Amount = 70000, TransactionReference = "SIM00001" }, true))
        {
            check(response.StatusCode == HttpStatusCode.OK, "Bank: authorized Development simulation 200");
            var simulated = (await response.Content.ReadFromJsonAsync<BankTransferResponse>())!;
            check(simulated.Source == "Simulation" && simulated.Amount == 50000 && simulated.ChangeAmount == 20000 && simulated.Note is not null,
                "Bank: excess retained separately with review warning");
        }
        var paid = await db.Invoices.AsNoTracking().SingleAsync(x => x.Id == invoice.Id);
        check(paid.PaidAmount == 100000 && paid.Status == InvoiceStatus.Paid, "Bank: mixed cash/bank never exceeds debt");
        var storedBank = await db.PaymentTransactions.AsNoTracking().SingleAsync(x => x.InvoiceId == invoice.Id && x.Source == "Simulation");
        check(storedBank.AmountTendered == 70000 && storedBank.BankReceivedAmount == 70000 && storedBank.ChangeAmount == 20000,
            "Bank: DL-191 stored tendered and change are exact");
        check(await db.PaymentTransactions.CountAsync(x => x.InvoiceId == invoice.Id) == 3, "Bank: immutable split payment count");
        check(await db.AuditLogs.CountAsync(x => x.EntityId == invoice.Id) == 4, "Bank: atomic payment audits plus excess warning; replay ignored");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Apikey", webhookKey);
        using (var response = await Post(body)) check(response.StatusCode == HttpStatusCode.OK, "Bank: callback replay after Paid");
        using (var response = await Post(body with { TransactionReference = "BANK0002" })) check(response.StatusCode == HttpStatusCode.Conflict, "Bank: new credit against Paid rejected");
        var raceInvoice = new Invoice { InvoiceCode = "INV-20261011-9002", TotalAmount = 50000, Status = InvoiceStatus.PendingPayment,
            CreatedByUserId = actor, CreatedAt = DateTime.UtcNow, Visit = new Visit { PatientId = patientId, CreatedByUserId = actor,
                Status = VisitStatuses.Completed, IsLocked = true } };
        db.Invoices.Add(raceInvoice); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var raceRequest = body with { AddInfo = "PKNK " + raceInvoice.InvoiceCode, Amount = 50000, TransactionReference = "RACE0001" };
        var concurrent = await Task.WhenAll(Post(raceRequest), Post(raceRequest));
        Console.WriteLine("Bank concurrent HTTP statuses: " + string.Join(", ", concurrent.Select(x => (int)x.StatusCode)));
        try { check(concurrent.Any(x => x.StatusCode == HttpStatusCode.OK) && concurrent.All(x => x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict), "Bank: concurrent duplicate callback is safely serialized"); }
        finally { foreach (var response in concurrent) response.Dispose(); }
        using (var response = await Post(raceRequest)) check(response.StatusCode == HttpStatusCode.OK, "Bank: concurrent retry returns committed receipt");
        check(await db.PaymentTransactions.CountAsync(x => x.InvoiceId == raceInvoice.Id) == 1 &&
            (await db.Invoices.AsNoTracking().SingleAsync(x => x.Id == raceInvoice.Id)).PaidAmount == 50000,
            "Bank: concurrent callbacks create one charge only");
        var actorRow = await db.Users.SingleAsync(x => x.UserId == actor); actorRow.IsActive = false; await db.SaveChangesAsync();
        using (var response = await Post(body)) check(response.StatusCode == HttpStatusCode.Unauthorized, "Bank: disabled configured actor fails closed");
    }
}
