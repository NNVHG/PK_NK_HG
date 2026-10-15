using System.Net;
using System.Net.Http.Json;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

internal static class InvoiceDiscountHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, int patientId,
        Action<string> authenticate, Action<bool, string> check)
    {
        const string catalog = "/api/invoices/discount-presets";
        var presetInput = new DiscountPresetRequest("synthetic25", "Synthetic 25 percent", "Percentage", 25, true);
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync(catalog)) check(response.StatusCode == HttpStatusCode.Unauthorized, "Discount anonymous 401");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role);
            using var read = await client.GetAsync(catalog);
            using var write = await client.PostAsJsonAsync("/api/invoices/1/discounts", new ApplyInvoiceDiscountRequest("Remove", null, null, null, "Synthetic", 0, 0, 0));
            check(read.StatusCode == HttpStatusCode.Forbidden && write.StatusCode == HttpStatusCode.Forbidden, $"Discount {role} read/write forbidden");
        }
        authenticate(RoleCodes.Receptionist);
        using (var response = await client.PostAsJsonAsync(catalog, presetInput)) check(response.StatusCode == HttpStatusCode.Forbidden, "Receptionist cannot configure presets");
        authenticate(RoleCodes.Admin);
        DiscountPresetResponse preset;
        using (var response = await client.PostAsJsonAsync(catalog, presetInput))
        {
            check(response.IsSuccessStatusCode, "Admin creates a real persisted preset");
            preset = (await response.Content.ReadFromJsonAsync<DiscountPresetResponse>())!;
            check(preset.Id > 0 && preset.Code == "SYNTHETIC25", "Preset canonical code and generated ID");
        }
        using (var response = await client.PostAsJsonAsync(catalog, presetInput)) check(response.StatusCode == HttpStatusCode.Conflict, "Duplicate preset code conflict");
        using (var response = await client.PostAsJsonAsync(catalog, presetInput with { Code = "INVALID", Value = 100.01m })) check(response.StatusCode == HttpStatusCode.BadRequest, "Percentage bound validated");
        authenticate(RoleCodes.Receptionist);
        using (var response = await client.GetAsync(catalog)) check(response.Headers.CacheControl?.NoStore == true &&
            (await response.Content.ReadFromJsonAsync<DiscountPresetResponse[]>())!.Single().Id == preset.Id, "Cashier reads active presets with no-store");

        await using var setup = new DentalDbContext(options);
        async Task<Invoice> Create(string suffix, decimal service = 100005, decimal medicine = 200000)
        {
            var invoice = new Invoice { Visit = new() { PatientId = patientId, CreatedByUserId = actorId,
                Status = VisitStatuses.Completed, IsLocked = true }, CreatedByUserId = actorId, CreatedAt = DateTime.UtcNow,
                InvoiceCode = "TEST-DISCOUNT-" + suffix, Status = InvoiceStatus.PendingPayment, TotalAmount = service + medicine,
                Items = [new() { ItemType = "Service", Code = "SYN", Name = "Synthetic service", Quantity = 1, UnitPrice = service, TotalAmount = service },
                    new() { ItemType = "Medicine", Code = "MED", Name = "Synthetic medicine", Quantity = 1, UnitPrice = medicine, TotalAmount = medicine }] };
            setup.Invoices.Add(invoice); await setup.SaveChangesAsync(); setup.ChangeTracker.Clear(); return invoice;
        }
        var cashInvoice = await Create("CASH");
        ApplyInvoiceDiscountRequest Request(Invoice invoice, decimal amount = 25001) => new("Preset", preset.Id, null, null,
            "Synthetic clinic discount", invoice.TotalAmount, 0, amount);
        var path = $"/api/invoices/{cashInvoice.Id}/discounts";
        using (var response = await client.PostAsJsonAsync(path, Request(cashInvoice) with { Reason = " " })) check(response.StatusCode == HttpStatusCode.BadRequest, "Reason required before any write");
        using (var response = await client.PostAsJsonAsync(path, Request(cashInvoice) with { Mode = "Custom", PresetId = null, DiscountType = "FixedAmount", Value = 100, ExpectedDiscountAmount = 100 }))
            check(response.StatusCode == HttpStatusCode.Forbidden, "Receptionist custom discount forbidden below limits");
        using (var response = await client.PostAsJsonAsync(path, Request(cashInvoice)))
        {
            var dto = await response.Content.ReadFromJsonAsync<InvoiceDiscountResponse>();
            check(response.IsSuccessStatusCode && dto?.DiscountAmount == 25001 && dto.FinalAmount == 275004, "Cashier preset rounds 25001.25 to 25001, excludes 200000 medicine");
        }
        var stored = await setup.Invoices.AsNoTracking().SingleAsync(x => x.Id == cashInvoice.Id);
        check(stored.DiscountAmount == 25001 && stored.DiscountValue == 25 && stored.DiscountPresetId == preset.Id, "Discount snapshot numeric values persisted");
        check(await setup.AuditLogs.AnyAsync(x => x.EntityId == cashInvoice.Id && x.Action == "MOD_BIL_DISCOUNT_APPLIED" && x.UserId == actorId), "Discount actor/reason audit persisted");
        using (var response = await client.PostAsJsonAsync(path, Request(cashInvoice))) check(response.StatusCode == HttpStatusCode.Conflict, "Stale financial snapshot rejected without retry");
        using (var response = await client.GetAsync($"/api/invoices/{cashInvoice.Id}/bank-transfer-info"))
        {
            // Config may be absent; the response still quotes the exact outstanding amount.
            var dto = await response.Content.ReadFromJsonAsync<BankTransferInfo>();
            check(response.IsSuccessStatusCode && dto?.RemainingAmount == 275004, "VietQR quotes discounted outstanding amount");
        }
        using (var response = await client.PostAsJsonAsync($"/api/invoices/{cashInvoice.Id}/payments", new CashPaymentRequest(300005, 300005, Guid.NewGuid())))
            check(response.StatusCode == HttpStatusCode.BadRequest, "Cash cannot collect old gross amount after discount");
        CashPaymentResponse payment;
        using (var response = await client.PostAsJsonAsync($"/api/invoices/{cashInvoice.Id}/payments", new CashPaymentRequest(275004, 300000, Guid.NewGuid())))
        {
            payment = (await response.Content.ReadFromJsonAsync<CashPaymentResponse>())!;
            check(response.IsSuccessStatusCode && payment.ChangeAmount == 24996, "Cash charges discounted balance and returns correct change");
        }
        using (var response = await client.GetAsync($"/api/invoices/{cashInvoice.Id}/payments/{payment.Id}/receipt"))
        {
            var receipt = await response.Content.ReadFromJsonAsync<ReceiptPrintDto>();
            check(response.IsSuccessStatusCode && receipt?.SubTotalAmount == 300005 && receipt.DiscountAmount == 25001 &&
                receipt.FinalAmount == 275004 && receipt.RemainingAmount == 0, "Receipt gross/discount/final amount reconcile");
        }
        using (var response = await client.PostAsJsonAsync(path, Request(cashInvoice) with { ExpectedCurrentDiscountAmount = 25001 }))
            check(response.StatusCode == HttpStatusCode.Conflict, "Any received payment locks discount editing");

        var bankInvoice = await Create("BANK", 100006, 200000);
        authenticate(RoleCodes.Admin);
        using (var response = await client.PostAsJsonAsync($"/api/invoices/{bankInvoice.Id}/discounts", Request(bankInvoice, 50003) with {
            Mode = "Custom", PresetId = null, DiscountType = "Percentage", Value = 50 }))
            check(response.IsSuccessStatusCode, "Admin custom percentage above limit applies");
        await using (var db = new DentalDbContext(options))
        {
            var result = await new BankTransferRepository(db).ReceiveAsync(new BankTransferRequest("PKNK " + bankInvoice.InvoiceCode, 300006, "SYN-DISCOUNT"),
                bankInvoice.InvoiceCode, actorId, true, null);
            check(result.IsSuccess && result.Value!.Amount == 250003 && result.Value.ChangeAmount == 50003, "Bank credits final balance; old gross excess flagged");
        }
        var unpaid = await Create("REMOVE", 100006, 0);
        using (var response = await client.PostAsJsonAsync($"/api/invoices/{unpaid.Id}/discounts", Request(unpaid, 25002))) check(response.IsSuccessStatusCode, "Half-dong rounds away from zero in PostgreSQL flow");
        using (var response = await client.PostAsJsonAsync($"/api/invoices/{unpaid.Id}/discounts", new ApplyInvoiceDiscountRequest("Remove", null, null, null,
            "Synthetic removal", unpaid.TotalAmount, 25002, 0))) check(response.IsSuccessStatusCode, "Discount removed with explicit reason and current snapshot");
        check((await setup.Invoices.AsNoTracking().SingleAsync(x => x.Id == unpaid.Id)).DiscountAmount == 0, "Removed discount persisted at zero");

        using (var response = await client.PutAsJsonAsync(catalog + "/" + preset.Id, presetInput with { Value = 20 })) check(response.IsSuccessStatusCode, "Admin updates preset value");
        authenticate(RoleCodes.Receptionist);
        using (var response = await client.PostAsJsonAsync($"/api/invoices/{unpaid.Id}/discounts", Request(unpaid, 25002)))
            check(response.StatusCode == HttpStatusCode.Conflict, "Changed preset cannot silently apply a different preview amount");

        authenticate(RoleCodes.Admin);
        using (var response = await client.PutAsJsonAsync(catalog + "/" + preset.Id, presetInput with { IsActive = false })) check(response.IsSuccessStatusCode, "Admin deactivates preset");
        authenticate(RoleCodes.Receptionist);
        using (var response = await client.GetAsync(catalog)) check((await response.Content.ReadFromJsonAsync<DiscountPresetResponse[]>())!.Length == 0, "Inactive presets hidden from cashier");
        using (var response = await client.PostAsJsonAsync($"/api/invoices/{unpaid.Id}/discounts", Request(unpaid, 25002))) check(response.StatusCode == HttpStatusCode.Conflict, "Inactive preset cannot be applied by ID");
        check((await setup.Invoices.AsNoTracking().SingleAsync(x => x.Id == cashInvoice.Id)).DiscountAmount == 25001, "Preset changes never rewrite past invoice discount");

        var rollback = await Create("ROLLBACK", 100006, 0);
        var rollbackRequest = new ApplyInvoiceDiscountRequest("Custom", null, "Percentage", 25, "Synthetic rollback", rollback.TotalAmount, 0, 25002);
        await using (var db = new DentalDbContext(options))
        {
            try { await new InvoiceDiscountRepository(db).ApplyAsync(rollback.Id, rollbackRequest, int.MaxValue, RoleCodes.Admin, null); check(false, "Expected audit FK failure"); }
            catch (DbUpdateException) { check(true, "Audit failure forces rollback"); }
        }
        check((await setup.Invoices.AsNoTracking().SingleAsync(x => x.Id == rollback.Id)).DiscountAmount == 0, "Audit failure leaves invoice unchanged");

        authenticate(RoleCodes.Admin);
        var race = await Create("CASH-RACE", 1000000, 0);
        var raceDiscount = new ApplyInvoiceDiscountRequest("Custom", null, "FixedAmount", 100000, "Synthetic race", race.TotalAmount, 0, 100000);
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"/api/invoices/{race.Id}/discounts", raceDiscount),
            client.PostAsJsonAsync($"/api/invoices/{race.Id}/payments", new CashPaymentRequest(1000000, 1000000, Guid.NewGuid())));
        try
        {
            check(responses.Count(x => x.IsSuccessStatusCode) == 1 && responses.All(x => x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict or HttpStatusCode.BadRequest),
                "Concurrent cash vs discount: one financial mutation succeeds, no 500");
            var state = await setup.Invoices.AsNoTracking().SingleAsync(x => x.Id == race.Id);
            check(state.PaidAmount <= state.FinalAmount && (state.DiscountAmount == 100000 && state.PaidAmount == 0 || state.DiscountAmount == 0 && state.PaidAmount == 1000000),
                "Concurrent charge never overpays a newly discounted invoice");
        }
        finally { foreach (var response in responses) response.Dispose(); }

        var free = await Create("ZERO", 1000000, 0);
        using (var response = await client.PostAsJsonAsync($"/api/invoices/{free.Id}/discounts", raceDiscount with { Value = 1000000, ExpectedDiscountAmount = 1000000 }))
            check(response.IsSuccessStatusCode, "Admin confirms full discount");
        var zero = await setup.Invoices.AsNoTracking().SingleAsync(x => x.Id == free.Id);
        check(zero.Status == InvoiceStatus.Paid && zero.FinalAmount == 0 && zero.PaidAmount == 0 &&
            !await setup.PaymentTransactions.AnyAsync(x => x.InvoiceId == free.Id), "Zero payable invoice settled without fabricated payment");

        var discountsRace = await Create("DISCOUNT-RACE", 1000000, 0);
        responses = await Task.WhenAll(client.PostAsJsonAsync($"/api/invoices/{discountsRace.Id}/discounts", raceDiscount),
            client.PostAsJsonAsync($"/api/invoices/{discountsRace.Id}/discounts", raceDiscount with { Value = 200000, ExpectedDiscountAmount = 200000 }));
        try
        {
            check(responses.Count(x => x.IsSuccessStatusCode) == 1 && responses.All(x => x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict),
                "Concurrent discount confirmations cannot overwrite one another");
            check(await setup.AuditLogs.CountAsync(x => x.EntityId == discountsRace.Id && x.Action == "MOD_BIL_DISCOUNT_APPLIED") == 1,
                "Only committed discount has an audit entry");
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
}
