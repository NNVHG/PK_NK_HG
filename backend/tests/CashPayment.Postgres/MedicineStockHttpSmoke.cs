using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Features.MOD_RX;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

internal static class MedicineStockHttpSmoke
{
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public string? Action { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Action is not null && data.Context!.ChangeTracker.Entries<AuditLog>().Any(a => a.State == EntityState.Added && a.Entity.Action == Action)) throw new InvalidOperationException("Synthetic audit failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
    private sealed class RaceGate : DbCommandInterceptor
    {
        private TaskCompletionSource? gate; private int arrivals;
        public void Arm() { arrivals = 0; gate = new(TaskCreationOptions.RunContinuationsAsynchronously); }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data, DbDataReader result, CancellationToken ct = default)
        {
            var current = gate;
            if (current is not null && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("FROM \"MedicineStockBalances\"", StringComparison.Ordinal))
            { if (Interlocked.Increment(ref arrivals) == 2) { gate = null; current.TrySetResult(); } await current.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); }
            return result;
        }
    }
    private sealed class ChunkedJson(string text) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(Encoding.UTF8.GetBytes(text)).AsTask();
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool, string> check)
    {
        var failure = new FailAudit(); var race = new RaceGate();
        var configured = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure, race).Options;
        await using var db = new DentalDbContext(configured);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Owned database required");
        var users = new Dictionary<string, User>();
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Receptionist, RoleCodes.Patient })
        { var user = new User { Role = new Role { RoleCode = role, RoleName = role }, FullName = "Synthetic stock actor", Phone = "096000000" + users.Count, PasswordHash = "synthetic-not-used" }; users[role] = user; }
        db.Users.AddRange(users.Values); await db.SaveChangesAsync(); var admin = users[RoleCodes.Admin].UserId; var doctor = users[RoleCodes.Dentist].UserId;
        var patient = new Patient { FullName = "Synthetic stock patient", Phone = "0960000008", UserId = users[RoleCodes.Patient].UserId, DateOfBirth = new DateOnly(1990, 1, 1) }; db.Patients.Add(patient);
        var medicine = new Medicine { Code = "M-STOCK", Name = "Synthetic medicine", Unit = "Viên", UnitPrice = 1000, IsActive = true, SearchText = "m stock synthetic medicine" };
        var second = new Medicine { Code = "M-SECOND", Name = "Synthetic second", Unit = "Viên", UnitPrice = 1000, IsActive = true, SearchText = "m second" };
        var inactive = new Medicine { Code = "M-OFF", Name = "Inactive", Unit = "Viên", UnitPrice = 0, IsActive = false };
        db.Medicines.AddRange(medicine, second, inactive); await db.SaveChangesAsync();
        var sequence = 0;
        async Task<Prescription> Order((Medicine Medicine, int Qty)[] lines, bool legacy = false, bool mismatchedUnit = false)
        {
            var total = lines.Sum(l => l.Medicine.UnitPrice * l.Qty);
            var visit = new Visit { PatientId = patient.PatientId, DentistId = doctor, CreatedByUserId = admin, Status = VisitStatuses.Completed, IsLocked = true };
            var invoice = new Invoice { Visit = visit, InvoiceCode = "MED-STOCK-" + (++sequence), CreatedByUserId = admin, TotalAmount = total, PaidAmount = total, Status = InvoiceStatus.Paid };
            var rx = new Prescription { Visit = visit, Invoice = invoice, PrescriptionCode = "RX-20261019-" + (9000 + sequence), DoctorId = doctor, CreatedByUserId = doctor, Diagnosis = "Synthetic saved content", CreatedAt = DateTime.UtcNow,
                DispensedAt = legacy ? DateTime.UtcNow : null, DispensedBy = legacy ? admin : null,
                Items = lines.Select(l => new PrescriptionItem { MedicineId = l.Medicine.Id, MedicineCode = l.Medicine.Code, MedicineName = l.Medicine.Name, Unit = mismatchedUnit ? "Hộp" : l.Medicine.Unit, Quantity = l.Qty, UnitPrice = l.Medicine.UnitPrice, UsageInstructions = "Synthetic instructions" }).ToArray() };
            db.Prescriptions.Add(rx); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return rx;
        }
        // Dữ liệu lịch sử tổng hợp trên baseline; không khẳng định đã kiểm nâng cấp migration cũ.
        var legacy = await Order([(medicine, 2)], true);
        var legacyTimestamp = await db.Prescriptions.AsNoTracking().Where(p => p.Id == legacy.Id).Select(p => p.DispensedAt).SingleAsync();
        await db.Database.MigrateAsync();
        check(await db.MedicineStockBalances.CountAsync() == 0 && await db.MedicineStockReceipts.CountAsync() == 0 && await db.MedicineStockIssues.CountAsync() == 0, "Baseline và chạy migration lại không sinh tồn kho từ lịch sử bàn giao");
        check((await db.Prescriptions.AsNoTracking().SingleAsync(p => p.Id == legacy.Id)).DispensedAt == legacyTimestamp, "Chạy migration lại giữ nguyên thời điểm bàn giao lịch sử");
        var clock = new VietnamClock(TimeProvider.System);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" }); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); builder.Configuration["Jwt:Secret"] = secret;
        builder.Services.AddScoped(_ => new DentalDbContext(configured)); builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddScoped<IMedicineStockRepository, MedicineStockRepository>(); builder.Services.AddScoped<MedicineStockService>();
        builder.Services.AddScoped<IMedicineCatalogRepository, MedicineCatalogRepository>(); builder.Services.AddScoped<MedicineCatalogService>();
        builder.Services.AddScoped<IPrescriptionRepository, PrescriptionRepository>(); builder.Services.AddScoped<PrescriptionService>();
        builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        builder.Services.AddScoped<IPrescriptionHandoverRepository, PrescriptionHandoverRepository>(); builder.Services.AddScoped<PrescriptionHandoverService>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>(); builder.Services.AddScoped<IAuditLogger, AuditLogger>(); builder.Services.AddSingleton(clock);
        builder.Services.AddControllers().AddApplicationPart(typeof(MedicineStockController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt => { jwt.TokenValidationParameters = new() { ValidateIssuer = true, ValidIssuer = "DentalClinic", ValidateAudience = true, ValidAudience = "DentalClinicClient", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), ClockSkew = TimeSpan.Zero }; jwt.Events = new() { OnTokenValidated = AccessTokenVersionValidation.ValidateAsync }; });
        builder.Services.AddAuthorization(o => o.AddApplicationPolicies());
        await using var app = builder.Build(); app.Use(async (c, next) => { try { await next(c); } catch { c.Response.StatusCode = 500; } }); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) }; var tokens = new JwtTokenService(builder.Configuration);
        void As(string? role) => client.DefaultRequestHeaders.Authorization = role is null ? null : new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(users[role]));
        MedicineStockReceiptRequest Receipt(int quantity = 10) => new(Guid.NewGuid(), quantity, 1, "Synthetic supplier", null, true);
        string Stock(int id) => "/api/medicine-stock/" + id;
        Task<HttpResponseMessage> Receive(int id, MedicineStockReceiptRequest input) => client.PostAsJsonAsync(Stock(id) + "/receipts", input);
        async Task<int> OnHand(int id) => await db.MedicineStockBalances.Where(b => b.MedicineId == id).Select(b => (int?)b.OnHand).SingleOrDefaultAsync() ?? 0;
        string Handover(Prescription rx) => "/api/prescriptions/" + rx.Id + "/dispense";
        Task<HttpResponseMessage> Give(Prescription rx, int version = 1) => client.PostAsJsonAsync(Handover(rx), new PrescriptionDispenseRequest(version, true));
        async Task<PrescriptionHandoverStatus> Status(Prescription rx)
        { using var r = await client.GetAsync(Handover(rx)); check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true, "Scoped handover stock read NoStore200"); return (await r.Content.ReadFromJsonAsync<PrescriptionHandoverStatus>())!; }
        try
        {
            As(null); using (var r = await client.GetAsync(Stock(medicine.Id))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous stock401");
            using (var r = await Receive(medicine.Id, Receipt())) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous receipt401");
            foreach (var role in new[] { RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Receptionist, RoleCodes.Patient })
            { As(role); using var read = await client.GetAsync(Stock(medicine.Id)); using var receiptResponse = await Receive(medicine.Id, Receipt()); check(read.StatusCode == HttpStatusCode.Forbidden, role + " cannot read admin receipt book"); check(receiptResponse.StatusCode == HttpStatusCode.Forbidden, role + " cannot receive medicine"); }
            As(RoleCodes.Admin);
            using (var r = await client.GetAsync(Stock(medicine.Id))) { var data = (await r.Content.ReadFromJsonAsync<MedicineStockDetail>())!; check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true && data.OnHand == 0 && data.Receipts.Length == 0, "Admin sees actual zero with no fake receipt"); }
            using (var r = await client.GetAsync(Stock(medicine.Id) + "?beforeId=-1")) check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid history cursor400");
            using (var r = await Receive(int.MaxValue, Receipt())) check(r.StatusCode == HttpStatusCode.NotFound, "Unknown medicine404");
            using (var r = await Receive(inactive.Id, Receipt())) check(r.StatusCode == HttpStatusCode.Conflict, "Inactive seed medicine cannot receive until reviewed");
            foreach (var input in new[] { Receipt(0), Receipt(-1), Receipt() with { Confirmed = false }, Receipt() with { RequestId = Guid.Empty }, Receipt() with { ExpectedMedicineVersion = 0 }, Receipt() with { SupplierName = " " }, Receipt() with { SupplierName = new string('x', 201) }, Receipt() with { Notes = new string('x', 501) } })
            { using var r = await Receive(medicine.Id, input); check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid receipt cannot change stock"); }
            using (var r = await Receive(medicine.Id, Receipt() with { ExpectedMedicineVersion = 2 })) check(r.StatusCode == HttpStatusCode.Conflict, "Stale catalog version rejects receipt");
            using (var r = await client.PostAsync(Stock(medicine.Id) + "/receipts", new StringContent("{\"extra\":\"" + new string('x', 9000) + "\"}", Encoding.UTF8, "application/json"))) check(r.StatusCode == HttpStatusCode.RequestEntityTooLarge && r.Headers.CacheControl?.NoStore == true, "Known oversize8192 rejected413 NoStore");
            using (var body = new ChunkedJson("{\"extra\":\"" + new string('x', 9000) + "\"}")) { body.Headers.ContentType = new("application/json"); using var r = await client.PostAsync(Stock(medicine.Id) + "/receipts", body); check(r.StatusCode == HttpStatusCode.RequestEntityTooLarge, "Chunked cannot bypass receipt body limit"); }
            check(await OnHand(medicine.Id) == 0 && !await db.MedicineStockReceipts.AnyAsync(), "Rejected requests create no balance or documents");
            var open = new Visit { PatientId = patient.PatientId, DentistId = doctor, CreatedByUserId = admin, Status = VisitStatuses.InProgress, Diagnosis = "Synthetic visit diagnosis" }; db.Visits.Add(open); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var availability = $"/api/visits/{open.VisitId}/prescription/stock?medicineIds={medicine.Id}";
            foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Assistant, RoleCodes.Receptionist, RoleCodes.Patient }) { As(role); using var r = await client.GetAsync(availability); check(r.StatusCode == HttpStatusCode.Forbidden, role + " cannot use dentist draft stock lookup"); }
            As(RoleCodes.Dentist); using (var r = await client.GetAsync(availability)) { var data = (await r.Content.ReadFromJsonAsync<MedicineAvailability[]>())!; check(r.StatusCode == HttpStatusCode.OK && data.Single().OnHand == 0 && r.Headers.CacheControl?.NoStore == true, "Owned open visit gets real zero warning, NoStore"); }
            var foreignDoctor = new User { Role = await db.Roles.SingleAsync(r => r.RoleCode == RoleCodes.Dentist), Phone = "0960000009", FullName = "Synthetic foreign doctor", PasswordHash = "synthetic-not-used" };
            db.Users.Add(foreignDoctor); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GenerateAccessToken(foreignDoctor));
            using (var r = await client.GetAsync(availability)) check(r.StatusCode == HttpStatusCode.NotFound, "Another dentist cannot query stock through someone else's clinical visit");
            As(RoleCodes.Dentist);
            using (var r = await client.GetAsync(availability + "&medicineIds=" + medicine.Id)) check(r.StatusCode == HttpStatusCode.BadRequest, "Duplicate medicine query400");
            using (var r = await client.GetAsync($"/api/visits/{legacy.VisitId}/prescription/stock?medicineIds={medicine.Id}")) check(r.StatusCode == HttpStatusCode.NotFound, "Closed visit cannot use prescribing stock lookup");
            var write = new PrescriptionWriteRequest(0, open.Diagnosis!, [new(medicine.Id, 1, 2, "Synthetic instructions")]);
            using var previewResponse = await client.PostAsJsonAsync($"/api/visits/{open.VisitId}/prescription/preview", write);
            check(previewResponse.StatusCode == HttpStatusCode.OK, "Zero stock does not block clinical preview");
            var preview = (await previewResponse.Content.ReadFromJsonAsync<PrescriptionPreview>())!;
            using (var r = await client.PostAsJsonAsync($"/api/visits/{open.VisitId}/prescription", write with { PreviewToken = preview.Token })) check(r.StatusCode == HttpStatusCode.Created, "Zero stock does not block saving prescription");
            check(await OnHand(medicine.Id) == 0 && !await db.MedicineStockIssues.AnyAsync(), "Prescribing never reserves or deducts stock");
            As(RoleCodes.Receptionist); var empty = await Order([(medicine, 2)]);
            var state = await Status(empty); check(state.Stock is { CanFulfill: false, NeedsDeduction: true } && !state.CanDispense, "Paid new order at zero stock is not handover-ready");
            using (var r = await Give(empty)) check(r.StatusCode == HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("RX_STOCK_INSUFFICIENT"), "Insufficient stock hard-blocks confirmed handover409");
            check(!await db.PrescriptionDispenseEvents.AnyAsync(e => e.PrescriptionId == empty.Id) && await OnHand(medicine.Id) == 0, "Insufficient handover creates no event or stock issue");
            As(RoleCodes.Admin); var receipt = Receipt(10);
            using (var r = await Receive(medicine.Id, receipt)) check(r.StatusCode == HttpStatusCode.Created && r.Headers.CacheControl?.NoStore == true, "Admin confirmed receipt201 NoStore");
            using (var r = await Receive(medicine.Id, receipt)) check(r.StatusCode == HttpStatusCode.OK && (await r.Content.ReadFromJsonAsync<MedicineStockReceiptSaved>())!.Replayed, "Same request id replays without adding again");
            check(await OnHand(medicine.Id) == 10 && await db.MedicineStockReceipts.CountAsync() == 1, "Receipt quantity added exactly once");
            using (var r = await client.GetAsync(Stock(medicine.Id))) check((await r.Content.ReadFromJsonAsync<MedicineStockDetail>())!.Receipts.Single().MedicineId == medicine.Id, "Receipt book scoped to selected medicine");
            using (var r = await client.GetAsync(Stock(medicine.Id) + "?beforeId=" + (await db.MedicineStockReceipts.SingleAsync()).Id)) check((await r.Content.ReadFromJsonAsync<MedicineStockDetail>())!.Receipts.Length == 0, "History before-id cursor excludes current receipt");
            using (var r = await Receive(medicine.Id, receipt with { Quantity = 11 })) check(r.StatusCode == HttpStatusCode.Conflict, "Same key altered content409");
            var edit = new MedicineWriteRequest(medicine.Code, medicine.Name, null, null, "Hộp", 1000, IsActive: true, ExpectedVersion: 1);
            using (var r = await client.PutAsJsonAsync("/api/medicines/" + medicine.Id, edit)) check(r.StatusCode == HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("MST_083"), "Stock history freezes Unit in catalog API");
            As(RoleCodes.Receptionist); using (var r = await Give(empty)) check(r.StatusCode == HttpStatusCode.OK, "Receptionist handover and stock issue200");
            check(await OnHand(medicine.Id) == 8 && await db.MedicineStockIssues.CountAsync(i => i.PrescriptionId == empty.Id) == 1 && await db.AuditLogs.AnyAsync(a => a.Action == "MOD_RX_STOCK_ISSUED" && a.EntityId == empty.Id), "Handover decrements stock, creates issue and required audit together");
            using (var r = await Give(empty)) check(r.StatusCode == HttpStatusCode.Conflict && await OnHand(medicine.Id) == 8, "Stale replay never deducts twice");
            using (var r = await client.PostAsJsonAsync($"/api/prescriptions/{empty.Id}/undo-dispense", new PrescriptionUndoDispenseRequest(2, true, "Synthetic metadata correction"))) check(r.StatusCode == HttpStatusCode.OK && await OnHand(medicine.Id) == 8, "Undo metadata never restores stock");
            using (var r = await Give(empty, 3)) check(r.StatusCode == HttpStatusCode.OK && await OnHand(medicine.Id) == 8 && await db.MedicineStockIssues.CountAsync(i => i.PrescriptionId == empty.Id) == 1, "Metadata handover again never exports same medicine twice");
            As(RoleCodes.Patient); state = await Status(empty); check(state.Stock is null, "Patient handover response excludes internal stock levels");
            As(RoleCodes.Admin); state = await Status(legacy); check(state.Stock is { LegacyHandover: true, NeedsDeduction: false }, "Legacy handover marked without invented stock issue");
            using (var r = await client.PostAsJsonAsync($"/api/prescriptions/{legacy.Id}/undo-dispense", new PrescriptionUndoDispenseRequest(1, true, "Synthetic legacy correction"))) check(r.StatusCode == HttpStatusCode.OK, "Legacy metadata undo still allowed");
            using (var r = await Give(legacy, 2)) check(r.StatusCode == HttpStatusCode.OK && await OnHand(medicine.Id) == 8 && !await db.MedicineStockIssues.AnyAsync(i => i.PrescriptionId == legacy.Id), "Legacy metadata redo does not deduct retrospectively");
            var multi = await Order([(medicine, 2), (second, 2)]);
            using (var r = await Give(multi)) check(r.StatusCode == HttpStatusCode.Conflict, "One shortage blocks whole multi-medicine order");
            check(await OnHand(medicine.Id) == 8 && !await db.MedicineStockIssues.AnyAsync(i => i.PrescriptionId == multi.Id), "Multi-line rejection has no partial debit");
            failure.Action = "MOD_RX_STOCK_RECEIVED"; using (var r = await Receive(second.Id, Receipt(5))) check(r.StatusCode == HttpStatusCode.InternalServerError, "Receipt audit failure surfaces500"); failure.Action = null;
            check(await OnHand(second.Id) == 0 && !await db.MedicineStockReceipts.AnyAsync(r => r.MedicineId == second.Id), "Receipt required audit rollback leaves zero stock and no document");
            using (var r = await Receive(second.Id, Receipt(3))) check(r.StatusCode == HttpStatusCode.Created, "Seed second medicine through authorized API");
            failure.Action = "MOD_RX_STOCK_ISSUED"; using (var r = await Give(multi)) check(r.StatusCode == HttpStatusCode.InternalServerError, "Issue audit failure surfaces500"); failure.Action = null;
            check(await OnHand(medicine.Id) == 8 && await OnHand(second.Id) == 3 && !await db.PrescriptionDispenseEvents.AnyAsync(e => e.PrescriptionId == multi.Id) && !await db.MedicineStockIssues.AnyAsync(i => i.PrescriptionId == multi.Id) && (await db.Prescriptions.AsNoTracking().SingleAsync(p => p.Id == multi.Id)).Version == 1, "Issue audit failure rolls back all stock, event, flag and version");
            var firstOrder = await Order([(second, 2)]); var secondOrder = await Order([(second, 2)]); race.Arm();
            var racing = await Task.WhenAll(Give(firstOrder), Give(secondOrder)); check(racing.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && racing.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "Two competing handovers have one winner and one409"); foreach (var r in racing) r.Dispose();
            check(await OnHand(second.Id) == 1 && await db.MedicineStockIssues.CountAsync(i => i.MedicineId == second.Id) == 1, "Concurrent handovers never oversell or duplicate ledger");
            var simultaneous = Receipt(2); race.Arm(); var receives = await Task.WhenAll(Receive(second.Id, simultaneous), Receive(second.Id, simultaneous));
            check(receives.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && receives.All(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict or HttpStatusCode.OK), "Concurrent identical receipt commits once"); foreach (var r in receives) r.Dispose();
            using (var r = await Receive(second.Id, simultaneous)) check(r.StatusCode == HttpStatusCode.OK && await OnHand(second.Id) == 3, "Retry concurrent receipt resolves one addition");
            var concurrentOrder = await Order([(second, 2)]); var simultaneousAddition = Receipt(2); race.Arm();
            var receiveAndGive = await Task.WhenAll(Receive(second.Id, simultaneousAddition), Give(concurrentOrder));
            check(receiveAndGive[0].StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict && receiveAndGive[1].StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, "Receipt versus handover returns success or409, never partial500");
            var expectedRemaining = 3 + (receiveAndGive[0].StatusCode == HttpStatusCode.Created ? 2 : 0) - (receiveAndGive[1].StatusCode == HttpStatusCode.OK ? 2 : 0);
            check(await OnHand(second.Id) == expectedRemaining, "Concurrent receipt and handover preserve exact stock equation"); foreach (var r in receiveAndGive) r.Dispose();
            var unitMismatch = await Order([(medicine, 1)], mismatchedUnit: true);
            using (var r = await Give(unitMismatch)) check(r.StatusCode == HttpStatusCode.Conflict && await OnHand(medicine.Id) == 8, "Unit mismatch never silently converts stock quantities");
            As(RoleCodes.Assistant); var assistantOrder = await Order([(medicine, 1)]);
            using (var r = await Give(assistantOrder)) check(r.StatusCode == HttpStatusCode.OK && await OnHand(medicine.Id) == 7, "Assistant existing handover right deducts stock atomically");
            check(await db.MedicineStockIssues.AnyAsync(i => i.PrescriptionId == assistantOrder.Id && i.IssuedByUserId == users[RoleCodes.Assistant].UserId), "Stock issue records actual Assistant confirmer");
            As(RoleCodes.Admin);
            var renamed = edit with { Unit = "Viên", Name = "Renamed synthetic medicine", UnitPrice = 1200 };
            using (var r = await client.PutAsJsonAsync("/api/medicines/" + medicine.Id, renamed)) check(r.StatusCode == HttpStatusCode.OK, "Stock history still permits catalog name/price updates");
            using (var r = await Receive(medicine.Id, receipt)) check(r.StatusCode == HttpStatusCode.OK && await OnHand(medicine.Id) == 7, "Old confirmed receipt replays after catalog rename without addition");
            var raceMedicine = new Medicine { Code = "M-UNIT-RACE", Name = "Synthetic unit race", Unit = "Viên", UnitPrice = 1000, IsActive = true }; db.Medicines.Add(raceMedicine); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            race.Arm(); var changingUnit = client.PutAsJsonAsync("/api/medicines/" + raceMedicine.Id, new MedicineWriteRequest(raceMedicine.Code, raceMedicine.Name, null, null, "Hộp", 1000, IsActive: true, ExpectedVersion: 1));
            var competingReceipt = Receive(raceMedicine.Id, Receipt(2)); var unitRace = await Task.WhenAll(changingUnit, competingReceipt);
            check(unitRace.Count(r => r.IsSuccessStatusCode) == 1 && unitRace.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "Unit change versus first receipt has one winner"); foreach (var r in unitRace) r.Dispose();
            var raceUnit = await db.Medicines.Where(m => m.Id == raceMedicine.Id).Select(m => m.Unit).SingleAsync();
            check(!await db.MedicineStockReceipts.AnyAsync(r => r.MedicineId == raceMedicine.Id && r.Unit != raceUnit), "No receipt can commit with a conflicting current stock unit");
            var full = new Medicine { Code = "M-LIMIT", Name = "Synthetic limit", Unit = "Viên", UnitPrice = 1000, IsActive = true }; db.Medicines.Add(full); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            using (var r = await Receive(full.Id, Receipt(int.MaxValue))) check(r.StatusCode == HttpStatusCode.Created, "Maximum integer stock receipt accepted");
            using (var r = await Receive(full.Id, Receipt(1))) check(r.StatusCode == HttpStatusCode.Conflict && await OnHand(full.Id) == int.MaxValue, "Stock addition overflow rejects without changing balance");
            var persistedReceipt = await db.MedicineStockReceipts.FirstAsync(); persistedReceipt.Quantity++;
            try { await db.SaveChangesAsync(); check(false, "Receipt immutable"); } catch (InvalidOperationException) { check(true, "Confirmed medicine receipt cannot be edited"); } db.ChangeTracker.Clear();
            var persistedIssue = await db.MedicineStockIssues.FirstAsync(); db.MedicineStockIssues.Remove(persistedIssue);
            try { await db.SaveChangesAsync(); check(false, "Issue immutable"); } catch (InvalidOperationException) { check(true, "Medicine stock issue cannot be deleted"); } db.ChangeTracker.Clear();
            check((await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == empty.InvoiceId)).PaidAmount == 2000 && !await db.PaymentTransactions.AnyAsync(), "Stock operations do not mutate payments or invoice amounts");
            var actor = await db.Users.SingleAsync(u => u.UserId == admin); actor.IsActive = false; await db.SaveChangesAsync();
            using (var r = await Receive(medicine.Id, Receipt())) check(r.StatusCode == HttpStatusCode.Forbidden, "Inactive Admin old JWT cannot receive");
            using (var r = await client.GetAsync(Stock(medicine.Id))) check(r.StatusCode == HttpStatusCode.Forbidden, "Inactive Admin old JWT cannot read receipt book");
            check(!await db.AuditLogs.AnyAsync(a => (a.Action == "MOD_RX_STOCK_ISSUED" || a.Action == "MOD_RX_STOCK_RECEIVED") && (a.Detail!.Contains(patient.FullName) || a.Detail.Contains(patient.Phone))), "Stock audit excludes patient and receiver data");
        }
        finally { await app.StopAsync(); }
    }
}
