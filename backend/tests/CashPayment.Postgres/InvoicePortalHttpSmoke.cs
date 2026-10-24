using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
internal static class InvoicePortalHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actor,
        Action<string> authenticate, Action<string, int> authenticateActor, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Portal fixtures require owned isolated database.");
        var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleCode == RoleCodes.Patient)
            ?? new Role { RoleCode = RoleCodes.Patient, RoleName = "Synthetic portal patient" };
        User Owner(string name) => new() { FullName = name, Role = role, Phone = "07" + Random.Shared.Next(10000000, 99999999),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), IsActive = true };
        var owner = Owner("Synthetic portal account A"); var other = Owner("Synthetic portal account B");
        var archivedOwner = Owner("Synthetic archived portal account"); var emptyOwner = Owner("Synthetic portal account without profile");
        var filterOwner = Owner("Synthetic filter account A"); var filterOther = Owner("Synthetic filter account B");
        db.Users.AddRange(owner, other, archivedOwner, emptyOwner, filterOwner, filterOther); await db.SaveChangesAsync();
        var sharedPhone = "03" + Random.Shared.Next(10000000, 99999999);
        Patient Profile(string name, User user, bool active = true) => new() { FullName = name, UserId = user.UserId, Phone = sharedPhone,
            DateOfBirth = new DateOnly(2000, 1, 1), IsActive = active };
        var a = Profile("Synthetic account A patient", owner);
        var foreign = Profile("Synthetic account B patient", other); var inactive = Profile("Synthetic archived patient", archivedOwner, false);
        db.Patients.AddRange(a, foreign, inactive); await db.SaveChangesAsync();
        var now = DateTime.UtcNow; var prefix = "PORTAL-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Invoice Make(string code, Patient patient, InvoiceStatus status, decimal paid = 0) => new() { InvoiceCode = prefix + code,
            TotalAmount = 100000, PaidAmount = paid, Status = status, CreatedAt = now, CreatedByUserId = actor,
            Visit = new Visit { PatientId = patient.PatientId, CreatedByUserId = actor, Status = VisitStatuses.Completed, IsLocked = true } };
        var invoice = Make("-A", a, InvoiceStatus.PendingPayment); var otherInvoice = Make("-B", foreign, InvoiceStatus.PendingPayment);
        var outsider = Make("-F", foreign, InvoiceStatus.PendingPayment); var archived = Make("-I", inactive, InvoiceStatus.PendingPayment);
        var cancelled = Make("-OLD", a, InvoiceStatus.Cancelled); cancelled.Visit = invoice.Visit; cancelled.CreatedAt = now.AddDays(-1);
        db.Invoices.AddRange(invoice, otherInvoice, outsider, archived, cancelled); await db.SaveChangesAsync();
        var cash = new PaymentTransaction { InvoiceId = invoice.Id, Amount = 40000, AmountTendered = 50000, ChangeAmount = 10000,
            PaymentMethod = "Cash", CashierId = actor, PaidAt = now.AddMinutes(-2), RequestId = Guid.NewGuid() };
        var bank = new PaymentTransaction { InvoiceId = invoice.Id, Amount = 30000, BankReceivedAmount = 30000, PaymentMethod = "BankTransfer",
            CashierId = actor, PaidAt = now.AddMinutes(-1), RequestId = Guid.NewGuid(), Source = "Simulation",
            TransactionReference = "SIM" + Guid.NewGuid().ToString("N")[..8], Note = "Synthetic private cashier review" };
        InvoiceItem Item(Invoice target, string code, string name, int quantity, decimal unitPrice) => new() {
            InvoiceId = target.Id, SourceVisitServiceId = int.MaxValue, Code = code, Name = name, Quantity = quantity,
            UnitPrice = unitPrice, TotalAmount = quantity * unitPrice, ToothNumber = 11, Surface = "buccal" };
        var firstItem = Item(invoice, "SNAP-A", "Synthetic stored service A", 2, 30000);
        var secondItem = Item(invoice, "SNAP-B", "Synthetic stored service B", 1, 40000);
        var oldItem = Item(cancelled, "SNAP-OLD", "Synthetic cancelled snapshot", 3, 60000);
        var foreignItem = Item(outsider, "SNAP-FOREIGN", "Synthetic other account snapshot", 1, 100000);
        var inactiveItem = Item(archived, "SNAP-ARCHIVED", "Synthetic archived snapshot", 1, 100000);
        db.InvoiceItems.AddRange(firstItem, secondItem, oldItem, foreignItem, inactiveItem); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        // Tạo đủ dòng snapshot trước khi thu tiền để tuân thủ bất biến hóa đơn đã thanh toán.
        var payable = await db.Invoices.SingleAsync(i => i.Id == invoice.Id);
        payable.Status = InvoiceStatus.PartiallyPaid; payable.PaidAmount = 70000;
        var paidOutsider = await db.Invoices.SingleAsync(i => i.Id == outsider.Id);
        paidOutsider.Status = InvoiceStatus.Paid; paidOutsider.PaidAmount = 100000;
        db.PaymentTransactions.AddRange(cash, bank); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var itemCount = await db.InvoiceItems.CountAsync();
        var audits = await db.AuditLogs.CountAsync(); var transactions = await db.PaymentTransactions.CountAsync(); var invoiceCount = await db.Invoices.CountAsync();
        string List(int id, string rest = "") => "/api/portal/invoices?patientId=" + id + rest;
        string Items(int id, int patientId, string rest = "") => "/api/portal/invoices/" + id + "/items?patientId=" + patientId + rest;
        string Payments(int id, int patientId, string rest = "") => "/api/portal/invoices/" + id + "/payments?patientId=" + patientId + rest;
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(List(a.PatientId))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Portal list anonymous401");
        using (var r = await client.GetAsync(Payments(invoice.Id, a.PatientId))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Portal payments anonymous401");
        foreach (var denied in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticateActor(denied, owner.UserId);
            using var list = await client.GetAsync(List(a.PatientId)); using var payments = await client.GetAsync(Payments(invoice.Id, a.PatientId));
            check(list.StatusCode == HttpStatusCode.Forbidden && payments.StatusCode == HttpStatusCode.Forbidden, "Portal role403 " + denied);
        }
        authenticateActor(RoleCodes.Patient, owner.UserId);
        foreach (var raw in new[] { "", "0", "-1", "abc", "1.0", "2147483648", foreign.PatientId.ToString(), int.MaxValue.ToString() })
        {
            using var list = await client.GetAsync("/api/portal/invoices?patientId=" + raw);
            using var payments = await client.GetAsync("/api/portal/invoices/" + invoice.Id + "/payments?patientId=" + raw);
            check(list.StatusCode == HttpStatusCode.Forbidden && payments.StatusCode == HttpStatusCode.Forbidden, "Portal invalid/foreign explicit profile403");
        }
        using (var r = await client.GetAsync("/api/patients/me"))
        { check(r.StatusCode == HttpStatusCode.OK, "Hồ sơ cá nhân trả HTTP 200"); var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(json.ValueKind == JsonValueKind.Object && json.GetProperty("patientId").GetInt32() == a.PatientId, "Portal account has one active patient profile"); }
        using (var r = await client.GetAsync(List(a.PatientId)))
        {
            var page = (await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>())!;
            check(r.StatusCode == HttpStatusCode.OK && page.TotalCount == 2 && page.Items.All(i => i.PatientCode == a.PatientCode), "Portal profile predicate before count");
            check(page.Items.Select(i => i.InvoiceId).SequenceEqual(new[] { invoice.Id, cancelled.Id }) && page.Items[1].Status == 4, "Portal preserves cancelled invoice and order");
            check(r.Headers.CacheControl?.NoStore == true && page.Items[0].PaidAmount == 70000 && page.Items[0].RemainingAmount == 30000, "Portal no-store and exact debt");
        }
        using (var r = await client.GetAsync(List(foreign.PatientId))) check(r.StatusCode == HttpStatusCode.Forbidden, "Portal account cannot select another patient's profile");
        using (var r = await client.GetAsync(List(inactive.PatientId))) check(r.StatusCode == HttpStatusCode.Forbidden, "Portal account cannot select an archived profile owned by another account");
        using (var r = await client.GetAsync(List(a.PatientId, "&page=2&pageSize=1&userId=" + other.UserId + "&patientCode=" + foreign.PatientCode)))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.TotalCount == 2 && page.Items.Single().InvoiceId == cancelled.Id, "Portal query spoof cannot override JWT/profile scope or count"); }
        foreach (var query in new[] { "&page=0", "&page=1000001", "&pageSize=0", "&pageSize=51" })
        {
            using var list = await client.GetAsync(List(a.PatientId, query)); using var payments = await client.GetAsync(Payments(invoice.Id, a.PatientId, query));
            check(list.StatusCode == HttpStatusCode.BadRequest && payments.StatusCode == HttpStatusCode.BadRequest, "Portal bounded page400");
        }
        foreach (var pair in new[] { (outsider.Id, a.PatientId), (invoice.Id, foreign.PatientId), (int.MaxValue, a.PatientId) })
        { using var r = await client.GetAsync(Payments(pair.Item1, pair.Item2)); check(r.StatusCode == HttpStatusCode.Forbidden, "Portal wrong/foreign/missing invoice403 same response"); }
        using (var r = await client.GetAsync(Payments(0, a.PatientId))) check(r.StatusCode == HttpStatusCode.BadRequest, "Portal invalid invoice400");
        using (var r = await client.GetAsync(Payments(invoice.Id, a.PatientId, "&pageSize=1")))
        {
            var page = (await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalPaymentResponse>>())!;
            check(page.TotalCount == 2 && page.TotalPages == 2 && page.Items.Single().PaymentId == bank.Id && page.Items.Single().IsSimulation, "Portal payments scoped order/count/simulation");
            check(r.Headers.CacheControl?.NoStore == true && page.Items.Single().Amount == 30000, "Portal payments no-store actual credited amount");
        }
        using (var r = await client.GetAsync(Payments(invoice.Id, a.PatientId, "&page=2&pageSize=1")))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalPaymentResponse>>(); check(page?.Items.Single().PaymentId == cash.Id && !page.Items.Single().IsSimulation, "Portal second cash transaction page"); }
        using (var r = await client.GetAsync(Payments(otherInvoice.Id, foreign.PatientId)))
        { check(r.StatusCode == HttpStatusCode.Forbidden, "Portal cannot read another account payment history"); }
        using (var r = await client.GetAsync(Payments(invoice.Id, a.PatientId)))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>(); var row = json.GetProperty("items")[0];
            check(row.EnumerateObject().Count() == 6 && !row.TryGetProperty("cashierId", out _) && !row.TryGetProperty("requestId", out _) &&
                !row.TryGetProperty("transactionReference", out _) && !row.TryGetProperty("note", out _), "Portal payment DTO excludes internal/contact fields");
        }
        using (var r = await client.GetAsync(List(a.PatientId)))
        { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); var row = json.GetProperty("items")[0]; check(row.EnumerateObject().Count() == 9 && !row.TryGetProperty("phone", out _), "Portal invoice DTO minimal"); }
        foreach (var endpoint in new[] { "/api/invoices", "/api/invoices/lookup?code=" + invoice.InvoiceCode, "/api/invoices/" + invoice.Id + "/payments", "/api/invoices/" + invoice.Id + "/payments/" + cash.Id + "/receipt" })
        { using var r = await client.GetAsync(endpoint); check(r.StatusCode == HttpStatusCode.Forbidden, "Portal patient still denied cashier endpoint"); }
        using (var r = await client.PostAsJsonAsync("/api/invoices/" + invoice.Id + "/payments", new CashPaymentRequest(10000, 10000, Guid.NewGuid(), "Cash"))) check(r.StatusCode == HttpStatusCode.Forbidden, "Portal patient cannot receive cash");
        authenticateActor(RoleCodes.Patient, other.UserId);
        using (var r = await client.GetAsync(List(a.PatientId))) check(r.StatusCode == HttpStatusCode.Forbidden, "Portal other account cannot use shared phone as ownership");
        using (var r = await client.GetAsync(List(foreign.PatientId))) { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.TotalCount == 2 && page.Items.Any(i => i.InvoiceId == outsider.Id) && page.Items.Any(i => i.InvoiceId == otherInvoice.Id), "Portal other account own list valid"); }
        var repo = new InvoicePortalRepository(db);
        check(await repo.OwnsPatientAsync(a.PatientId, owner.UserId), "Portal ownership initial check true");
        await db.Patients.Where(p => p.PatientId == a.PatientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, (int?)emptyOwner.UserId));
        var staleList = await repo.GetInvoicesAsync(a.PatientId, owner.UserId, 1, 20);
        check(staleList.TotalCount == 0 && staleList.Items.Count == 0, "Portal SQL rechecks ownership after earlier check");
        check(await repo.GetPaymentsAsync(invoice.Id, a.PatientId, owner.UserId, 1, 20) is null, "Portal payment SQL rejects changed ownership");
        await db.Patients.Where(p => p.PatientId == a.PatientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, (int?)owner.UserId));
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(Items(invoice.Id, a.PatientId))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Portal items anonymous401");
        foreach (var denied in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticateActor(denied, owner.UserId);
            using var r = await client.GetAsync(Items(invoice.Id, a.PatientId)); check(r.StatusCode == HttpStatusCode.Forbidden, "Portal items staff403 " + denied);
        }
        authenticateActor(RoleCodes.Patient, owner.UserId);
        foreach (var raw in new[] { "", "0", "-1", "abc", "1.0", "2147483648", foreign.PatientId.ToString(), int.MaxValue.ToString() })
        {
            using var r = await client.GetAsync("/api/portal/invoices/" + invoice.Id + "/items?patientId=" + raw);
            check(r.StatusCode == HttpStatusCode.Forbidden, "Portal items missing/invalid/foreign profile403");
        }
        foreach (var pair in new[] { (outsider.Id, a.PatientId), (invoice.Id, foreign.PatientId), (int.MaxValue, a.PatientId) })
        {
            using var r = await client.GetAsync(Items(pair.Item1, pair.Item2)); check(r.StatusCode == HttpStatusCode.Forbidden, "Portal items wrong/foreign/missing invoice403");
            var error = await r.Content.ReadFromJsonAsync<JsonElement>(); check(error.GetProperty("code").GetString() == "GEN_002" && !error.TryGetProperty("items", out _), "Portal items denied response opaque");
        }
        foreach (var id in new[] { 0, -1 }) { using var r = await client.GetAsync(Items(id, a.PatientId)); check(r.StatusCode == HttpStatusCode.BadRequest, "Portal items invalid invoice400"); }
        foreach (var query in new[] { "&page=0", "&page=1000001", "&pageSize=0", "&pageSize=51", "&page=no" })
        { using var r = await client.GetAsync(Items(invoice.Id, a.PatientId, query)); check(r.StatusCode == HttpStatusCode.BadRequest, "Portal items invalid page400"); }
        using (var r = await client.GetAsync(Items(invoice.Id, a.PatientId, "&pageSize=1&userId=" + other.UserId + "&invoiceId=" + outsider.Id)))
        {
            var page = (await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalItemResponse>>())!;
            check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true, "Portal items200 no-store");
            check(page.TotalCount == 2 && page.TotalPages == 2 && page.Items.Single().ItemId == firstItem.Id, "Portal items scope before count and stable ascending order");
            check(page.Items.Single() == new InvoicePortalItemResponse(firstItem.Id, firstItem.Code, firstItem.Name, 2, 30000, 60000), "Portal item exact stored snapshot no catalogue dependency");
        }
        using (var r = await client.GetAsync(Items(invoice.Id, a.PatientId, "&page=2&pageSize=1")))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalItemResponse>>(); check(page?.TotalCount == 2 && page.Items.Single().ItemId == secondItem.Id, "Portal second item page"); }
        using (var r = await client.GetAsync(Items(invoice.Id, a.PatientId, "&page=1000000&pageSize=50")))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalItemResponse>>(); check(page?.TotalCount == 2 && page.Items.Count == 0, "Portal empty far item page200"); }
        using (var r = await client.GetAsync(Items(otherInvoice.Id, foreign.PatientId)))
        { check(r.StatusCode == HttpStatusCode.Forbidden, "Portal cannot read another account invoice items"); }
        using (var r = await client.GetAsync(Items(cancelled.Id, a.PatientId)))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalItemResponse>>(); check(page?.Items.Single().ItemId == oldItem.Id && page.Items.Single().UnitPrice == 60000, "Portal cancelled invoice keeps separate old snapshot"); }
        authenticateActor(RoleCodes.Patient, archivedOwner.UserId);
        using (var r = await client.GetAsync(Items(archived.Id, inactive.PatientId)))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalItemResponse>>(); check(page?.Items.Single().ItemId == inactiveItem.Id, "Portal items owned inactive profile still available"); }
        authenticateActor(RoleCodes.Patient, owner.UserId);
        using (var r = await client.GetAsync(Items(invoice.Id, a.PatientId)))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>(); var row = json.GetProperty("items")[0];
            check(row.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "itemId", "code", "name", "quantity", "unitPrice", "totalAmount" }.Order()), "Portal item minimal six fields no clinical/source/staff fields");
        }
        authenticateActor(RoleCodes.Patient, other.UserId);
        using (var r = await client.GetAsync(Items(invoice.Id, a.PatientId))) check(r.StatusCode == HttpStatusCode.Forbidden, "Portal item shared phone is not ownership");
        using (var r = await client.GetAsync(Items(outsider.Id, foreign.PatientId)))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalItemResponse>>(); check(page?.Items.Single().ItemId == foreignItem.Id, "Portal other account own item succeeds"); }
        check(await repo.OwnsPatientAsync(a.PatientId, owner.UserId), "Portal item prior ownership true");
        await db.Patients.Where(p => p.PatientId == a.PatientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, (int?)emptyOwner.UserId));
        check(await repo.GetItemsAsync(invoice.Id, a.PatientId, owner.UserId, 1, 20) is null, "Portal item SQL rejects stale ownership");
        await db.Patients.Where(p => p.PatientId == a.PatientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, (int?)owner.UserId));
        check(await db.InvoiceItems.CountAsync() == itemCount && await db.Invoices.Where(i => i.Id == invoice.Id).AnyAsync(i => i.TotalAmount == 100000 && i.PaidAmount == 70000 && i.Status == InvoiceStatus.PartiallyPaid),
            "Portal item reads preserve snapshots invoice amounts/status");
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync("/api/portal/invoices/profiles")) check(r.StatusCode == HttpStatusCode.Unauthorized, "Portal profiles anonymous401");
        foreach (var denied in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticateActor(denied, owner.UserId);
            using var r = await client.GetAsync("/api/portal/invoices/profiles"); check(r.StatusCode == HttpStatusCode.Forbidden, "Portal profiles staff403 " + denied);
        }
        authenticateActor(RoleCodes.Patient, owner.UserId);
        using (var r = await client.GetAsync("/api/portal/invoices/profiles?userId=" + other.UserId + "&patientId=" + foreign.PatientId + "&isActive=true"))
        {
            var rows = (await r.Content.ReadFromJsonAsync<List<InvoicePortalProfileResponse>>())!;
            check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true, "Portal profiles200 no-store");
            check(rows.Select(p => p.PatientId).SequenceEqual(new[] { a.PatientId }), "Portal profile endpoint returns only the one account profile");
            check(rows.Single() == new InvoicePortalProfileResponse(a.PatientId, a.PatientCode, a.FullName, a.DateOfBirth, true), "Portal profile exact five values including birth/code");
        }
        using (var r = await client.GetAsync("/api/portal/invoices/profiles"))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            check(json[0].EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "patientId", "patientCode", "fullName", "dateOfBirth", "isActive" }.Order()), "Portal profile DTO excludes contact CCCD and internal identifiers");
        }
        using (var r = await client.GetAsync("/api/patients/me"))
        { check(r.StatusCode == HttpStatusCode.OK, "Hợp đồng hồ sơ cá nhân giữ HTTP 200"); var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(json.ValueKind == JsonValueKind.Object && json.GetProperty("patientId").GetInt32() == a.PatientId, "Patient active profile contract preserved"); }
        authenticateActor(RoleCodes.Patient, archivedOwner.UserId);
        using (var r = await client.GetAsync(List(inactive.PatientId)))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoiceLookupResponse>>(); check(page?.Items.Single().InvoiceId == archived.Id, "Selected archived profile own invoice accessible"); }
        using (var r = await client.GetAsync(Items(archived.Id, inactive.PatientId)))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalItemResponse>>(); check(page?.Items.Single().ItemId == inactiveItem.Id, "Selected archived profile own item accessible"); }
        using (var r = await client.GetAsync(Payments(archived.Id, inactive.PatientId)))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<InvoicePortalPaymentResponse>>(); check(page?.TotalCount == 0, "Selected archived profile payment history empty200"); }
        authenticateActor(RoleCodes.Patient, emptyOwner.UserId);
        using (var r = await client.GetAsync("/api/portal/invoices/profiles"))
        { var rows = await r.Content.ReadFromJsonAsync<List<InvoicePortalProfileResponse>>(); check(r.StatusCode == HttpStatusCode.OK && rows?.Count == 0, "Portal account without profile returns empty200"); }
        authenticateActor(RoleCodes.Patient, other.UserId);
        using (var r = await client.GetAsync("/api/portal/invoices/profiles"))
        { var rows = await r.Content.ReadFromJsonAsync<List<InvoicePortalProfileResponse>>(); check(rows?.Count == 1 && rows[0].PatientId == foreign.PatientId, "Portal other account receives only own profiles despite shared phone"); }
        check((await repo.GetProfilesAsync(owner.UserId)).Count == 1, "Portal profile repository ownership initial");
        var duplicateOwnerRejected = false;
        try { await db.Patients.Where(p => p.PatientId == inactive.PatientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, (int?)other.UserId)); }
        catch (PostgresException error) when (error.SqlState == "23505" && error.ConstraintName == "IX_Patients_UserId") { duplicateOwnerRejected = true; }
        check(duplicateOwnerRejected && (await repo.GetProfilesAsync(other.UserId)).Count == 1 &&
            await repo.OwnsPatientAsync(inactive.PatientId, archivedOwner.UserId), "CSDL từ chối tài khoản có nhiều hồ sơ và giữ nguyên chủ sở hữu");
        await db.Patients.Where(p => p.PatientId == inactive.PatientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, (int?)emptyOwner.UserId));
        check(await repo.GetItemsAsync(archived.Id, inactive.PatientId, archivedOwner.UserId, 1, 20) is null, "Portal financial read rejects stale archived selector after owner change");
        await db.Patients.Where(p => p.PatientId == inactive.PatientId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, (int?)archivedOwner.UserId));
        check(await db.Patients.Where(p => p.PatientId == inactive.PatientId).AnyAsync(p => !p.IsActive), "Portal read never reactivates archived profile");
        check(await db.AuditLogs.CountAsync() == audits && await db.PaymentTransactions.CountAsync() == transactions &&
            await db.Invoices.CountAsync() == invoiceCount, "Portal reads never mutate financial records or audit");
        await InvoicePortalFilterHttpSmoke.RunAsync(client, options, actor, filterOwner.UserId, filterOther.UserId, emptyOwner.UserId, authenticateActor, check);
        authenticate(RoleCodes.Admin);
    }
}
