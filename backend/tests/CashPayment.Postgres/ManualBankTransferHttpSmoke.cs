using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class ManualBankTransferHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actor, int patient,
        string webhookKey, Action<string, int> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var sequence = 8100;
        async Task<Invoice> InvoiceAsync(decimal discount = 0, InvoiceStatus status = InvoiceStatus.PendingPayment,
            string visitStatus = VisitStatuses.Completed, bool locked = true)
        {
            var invoice = new Invoice { InvoiceCode = $"INV-20261022-{++sequence:D4}", TotalAmount = 100000, DiscountAmount = discount,
                Status = status, CreatedByUserId = actor, CreatedAt = DateTime.UtcNow,
                Visit = new Visit { PatientId = patient, CreatedByUserId = actor, Status = visitStatus, IsLocked = locked } };
            db.Invoices.Add(invoice); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return invoice;
        }
        Task<HttpResponseMessage> Post(int id, ManualBankTransferRequest body)
            => client.PostAsJsonAsync($"/api/invoices/{id}/bank-transfer/confirm", body);
        var invoice = await InvoiceAsync(10000);
        var body = new ManualBankTransferRequest(Guid.NewGuid(), 30000, "MANU0001", 90000, true);
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await Post(invoice.Id, body)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Manual bank: anonymous rejected");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role, actor);
            using var r = await Post(invoice.Id, body); check(r.StatusCode == HttpStatusCode.Forbidden, "Manual bank: clinical/patient roles rejected");
        }
        authenticate(RoleCodes.Admin, actor);
        using (var r = await Post(invoice.Id, body)) check(r.StatusCode == HttpStatusCode.Forbidden, "Manual bank: forged/stale role cannot override actual DB role");
        authenticate(RoleCodes.Receptionist, actor);
        var user = await db.Users.SingleAsync(u => u.UserId == actor); user.IsActive = false; await db.SaveChangesAsync();
        using (var r = await Post(invoice.Id, body)) check(r.StatusCode == HttpStatusCode.Forbidden, "Manual bank: locked actor rejected");
        user.IsActive = true; user.RequiresPhoneActivation = true; await db.SaveChangesAsync();
        using (var r = await Post(invoice.Id, body)) check(r.StatusCode == HttpStatusCode.Forbidden, "Manual bank: actor awaiting activation rejected");
        user.RequiresPhoneActivation = false; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        foreach (var invalid in new[] { body with { Confirmed = false }, body with { RequestId = Guid.Empty }, body with { Amount = 0 },
            body with { Amount = 1.5m }, body with { ExpectedRemainingAmount = 0 }, body with { TransactionReference = "short" } })
        { using var r = await Post(invoice.Id, invalid); check(r.StatusCode == HttpStatusCode.BadRequest, "Manual bank: invalid amount/reference/snapshot/consent rejected"); }
        using (var r = await Post(0, body)) check(r.StatusCode == HttpStatusCode.BadRequest, "Manual bank: invalid invoice id rejected");
        using (var r = await Post(int.MaxValue, body)) check(r.StatusCode == HttpStatusCode.NotFound, "Manual bank: missing invoice rejected");
        BankTransferResponse receipt;
        using (var r = await Post(invoice.Id, body))
        {
            check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true, "Manual bank: real receipt available in Production, no bank configuration needed, NoStore");
            receipt = (await r.Content.ReadFromJsonAsync<BankTransferResponse>())!;
            check(receipt.Source == "Manual" && receipt.Amount == 30000 && receipt.BankReceivedAmount == 30000, "Manual bank: stores real source and received amount");
        }
        var paid = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id);
        check(paid.PaidAmount == 30000 && paid.FinalAmount == 90000 && paid.Status == InvoiceStatus.PartiallyPaid, "Manual bank: partial payment uses discounted balance");
        using (var r = await Post(invoice.Id, body)) check((await r.Content.ReadFromJsonAsync<BankTransferResponse>())!.Id == receipt.Id, "Manual bank: request replay returns original receipt");
        using (var r = await Post(invoice.Id, body with { RequestId = Guid.NewGuid() }))
            check((await r.Content.ReadFromJsonAsync<BankTransferResponse>())!.Id == receipt.Id, "Manual bank: same reference across fresh request id is not charged again");
        foreach (var changed in new[] { body with { Amount = 30001 }, body with { TransactionReference = "OTHER001" } })
        { using var r = await Post(invoice.Id, changed); check(r.StatusCode == HttpStatusCode.Conflict, "Manual bank: changed payload under reused key rejected"); }
        var other = await InvoiceAsync();
        using (var r = await Post(other.Id, body with { RequestId = Guid.NewGuid() }))
            check(r.StatusCode == HttpStatusCode.Conflict, "Manual bank: reference cannot move to another invoice");
        using (var r = await Post(invoice.Id, body with { RequestId = Guid.NewGuid(), TransactionReference = "STALE001" }))
            check(r.StatusCode == HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("BIL_064"), "Manual bank: stale balance rejected before any write");
        check(await db.PaymentTransactions.CountAsync(p => p.InvoiceId == invoice.Id) == 1 &&
            await db.AuditLogs.CountAsync(a => a.EntityId == invoice.Id && a.Action == "MOD_BIL_BANK_MANUAL_CONFIRMED") == 1, "Manual bank: replay/rejection does not duplicate payment or audit");

        var adminRole = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == RoleCodes.Admin) ?? new Role { RoleCode = RoleCodes.Admin, RoleName = "Synthetic Admin" };
        var admin = new User { Role = adminRole, FullName = "Synthetic manual bank admin", Phone = "0999988100", PasswordHash = "test-only-hash" };
        db.Users.Add(admin); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); authenticate(RoleCodes.Admin, admin.UserId);
        var final = new ManualBankTransferRequest(Guid.NewGuid(), 60000, "MANU0002", 60000, true);
        using (var r = await Post(invoice.Id, final)) check(r.StatusCode == HttpStatusCode.OK, "Manual bank: actual Admin can settle remaining debt");
        paid = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id);
        check(paid.PaidAmount == 90000 && paid.Status == InvoiceStatus.Paid, "Manual bank: subsequent receipt settles invoice");
        authenticate(RoleCodes.Receptionist, actor);
        using (var r = await Post(invoice.Id, body)) check(r.StatusCode == HttpStatusCode.OK, "Manual bank: committed request replay works after Paid");
        using (var r = await client.GetAsync($"/api/invoices/{invoice.Id}/payments/{receipt.Id}/receipt"))
            check(r.StatusCode == HttpStatusCode.OK, "Manual bank: receipt preview integrates with existing cashier receipt API");
        var excess = await InvoiceAsync();
        using (var r = await Post(excess.Id, new(Guid.NewGuid(), 120000, "MANUEX01", 100000, true)))
        {
            var p = await r.Content.ReadFromJsonAsync<BankTransferResponse>();
            check(r.StatusCode == HttpStatusCode.OK && p!.Amount == 100000 && p.ChangeAmount == 20000 && p.Note is not null, "Manual bank: existing bank excess rule retained separately");
        }
        check(await db.AuditLogs.CountAsync(a => a.EntityId == excess.Id && a.Action == "MOD_BIL_BANK_EXCESS_REVIEW") == 1, "Manual bank: excess review audited without automatic refund");
        foreach (var status in new[] { InvoiceStatus.Draft, InvoiceStatus.Cancelled, InvoiceStatus.Paid, InvoiceStatus.Refunded })
        {
            var closed = await InvoiceAsync(status: status);
            using var r = await Post(closed.Id, new(Guid.NewGuid(), 10, $"CLOSED{closed.Id}", 100000, true));
            check(r.StatusCode == HttpStatusCode.Conflict, "Manual bank: non-payable invoice rejected");
        }
        foreach (var visitState in new[] { VisitStatuses.InProgress, VisitStatuses.Correcting })
        {
            var open = await InvoiceAsync(visitStatus: visitState, locked: false);
            using var r = await Post(open.Id, new(Guid.NewGuid(), 10, $"VISIT_{open.Id}", 100000, true));
            check(r.StatusCode == HttpStatusCode.Conflict, "Manual bank: open/correcting visit cannot receive payment");
        }

        // Own and remove only configuration rows added by this synthetic fixture.
        var ownedKeys = new List<string>();
        foreach (var pair in new Dictionary<string, string> { ["Payment:BankBin"] = "999999", ["Payment:AccountNumber"] = "12345678", ["Payment:AccountName"] = "Synthetic bank" })
            if (!await db.SystemConfigs.AnyAsync(c => c.Key == pair.Key)) { db.SystemConfigs.Add(new() { Key = pair.Key, Value = pair.Value }); ownedKeys.Add(pair.Key); }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        async Task<HttpResponseMessage> Webhook(Invoice target, string reference, decimal amount)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Apikey", webhookKey);
            return await client.PostAsJsonAsync("/api/webhooks/payment/bank-transfer", new SePayWebhookRequest(900000 + target.Id, "in", amount, reference, "PKNK " + target.InvoiceCode, "12345678"));
        }
        try
        {
            using (var r = await Webhook(invoice, body.TransactionReference, body.Amount))
                check(r.StatusCode == HttpStatusCode.OK, "Manual bank: delayed webhook acknowledges manual transfer after Paid");
            using (var r = await Webhook(invoice, body.TransactionReference, body.Amount + 1))
                check(r.StatusCode == HttpStatusCode.Conflict, "Manual bank: webhook cannot reinterpret manual amount");
            using (var r = await Webhook(other, body.TransactionReference, body.Amount))
                check(r.StatusCode == HttpStatusCode.Conflict, "Manual bank: webhook cannot move manual reference");
            using (var r = await Post(other.Id, new(Guid.NewGuid(), 10, "KEYONLY1", 100000, true)))
                check(r.StatusCode == HttpStatusCode.Unauthorized, "Manual bank: API key never authorizes manual endpoint");
            using (var r = await Webhook(other, "WEBMAN01", 25000)) check(r.StatusCode == HttpStatusCode.OK, "Manual bank: optional webhook can arrive first");
            authenticate(RoleCodes.Receptionist, actor);
            using (var r = await Post(other.Id, new(Guid.NewGuid(), 25000, "WEBMAN01", 100000, true)))
                check(r.StatusCode == HttpStatusCode.OK && (await r.Content.ReadFromJsonAsync<BankTransferResponse>())!.Source == "Webhook", "Manual bank: manual confirmation preserves previously received webhook source");
            check(await db.PaymentTransactions.CountAsync(p => p.InvoiceId == other.Id) == 1, "Manual bank: webhook/manual confirmation stores one receipt");
            var simulation = await InvoiceAsync();
            await using (var simulationDb = new DentalDbContext(options))
                await new BankTransferRepository(simulationDb).ReceiveAsync(new("PKNK " + simulation.InvoiceCode, 20000, "SIMMAN01"), simulation.InvoiceCode, actor,
                    actorRole: null, simulated: true, manualConfirmation: false, ip: null);
            using (var r = await Post(simulation.Id, new(Guid.NewGuid(), 20000, "SIMMAN01", 100000, true)))
                check(r.StatusCode == HttpStatusCode.Conflict, "Manual bank: simulated transfer cannot be confirmed as real");

            var race = await InvoiceAsync(); var raceRequest = new ManualBankTransferRequest(Guid.NewGuid(), 40000, "RACEMAN1", 100000, true);
            var concurrent = await Task.WhenAll(Post(race.Id, raceRequest), Post(race.Id, raceRequest));
            try { check(concurrent.Any(r => r.StatusCode == HttpStatusCode.OK) && concurrent.All(r => r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict), "Manual bank: concurrent duplicate requests serialize safely"); }
            finally { foreach (var r in concurrent) r.Dispose(); }
            using (var r = await Post(race.Id, raceRequest)) check(r.StatusCode == HttpStatusCode.OK, "Manual bank: concurrency retry finds the committed request");
            check(await db.PaymentTransactions.CountAsync(p => p.InvoiceId == race.Id) == 1 &&
                (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == race.Id)).PaidAmount == 40000, "Manual bank: race cannot charge twice");

            var mixedRace = await InvoiceAsync(); var mixedRequest = new ManualBankTransferRequest(Guid.NewGuid(), 50000, "MIXRACE1", 100000, true);
            authenticate(RoleCodes.Receptionist, actor);
            using var manualClient = new HttpClient { BaseAddress = client.BaseAddress };
            manualClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
            var mixedResponses = await Task.WhenAll(manualClient.PostAsJsonAsync($"/api/invoices/{mixedRace.Id}/bank-transfer/confirm", mixedRequest), Webhook(mixedRace, mixedRequest.TransactionReference, mixedRequest.Amount));
            try { check(mixedResponses.Any(r => r.StatusCode == HttpStatusCode.OK) && mixedResponses.All(r => r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict), "Manual bank: simultaneous manual/webhook transfer safely serializes"); }
            finally { foreach (var r in mixedResponses) r.Dispose(); }
            using (var r = await manualClient.PostAsJsonAsync($"/api/invoices/{mixedRace.Id}/bank-transfer/confirm", mixedRequest))
                check(r.StatusCode == HttpStatusCode.OK, "Manual bank: manual retry reconciles either winning source");
            using (var r = await Webhook(mixedRace, mixedRequest.TransactionReference, mixedRequest.Amount))
                check(r.StatusCode == HttpStatusCode.OK, "Manual bank: webhook retry reconciles either winning source");
            check(await db.PaymentTransactions.CountAsync(p => p.InvoiceId == mixedRace.Id) == 1 &&
                (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == mixedRace.Id)).PaidAmount == 50000 &&
                await db.AuditLogs.CountAsync(a => a.EntityType == "Invoice" && a.EntityId == mixedRace.Id) == 1,
                "Manual bank: cross-source race preserves one receipt/credit/audit");

            var rollback = await InvoiceAsync();
            var failOptions = new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(new FailManualAudit()).Options;
            await using (var failDb = new DentalDbContext(failOptions))
            {
                var threw = false;
                try { await new BankTransferRepository(failDb).ConfirmManualAsync(rollback.Id, new(Guid.NewGuid(), 10, "FAILAUD1", 100000, true), actor, RoleCodes.Receptionist, null); }
                catch (InvalidOperationException e) when (e.Message == "Synthetic manual audit failure") { threw = true; }
                check(threw && !failDb.ChangeTracker.Entries().Any(), "Manual bank: failed required audit fails the transaction and clears tracked writes");
            }
            check(!await db.PaymentTransactions.AnyAsync(p => p.InvoiceId == rollback.Id) &&
                (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == rollback.Id)).PaidAmount == 0, "Manual bank: audit failure rolls back payment and invoice together");
        }
        finally
        {
            db.ChangeTracker.Clear(); db.SystemConfigs.RemoveRange(await db.SystemConfigs.Where(c => ownedKeys.Contains(c.Key)).ToArrayAsync());
            await db.SaveChangesAsync(); authenticate(RoleCodes.Receptionist, actor);
        }
    }

    private sealed class FailManualAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.Action == "MOD_BIL_BANK_MANUAL_CONFIRMED"))
                throw new InvalidOperationException("Synthetic manual audit failure");
            return base.SavingChangesAsync(eventData, result, ct);
        }
    }
}
