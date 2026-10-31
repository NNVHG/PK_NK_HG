using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Application.Common;
using Dental.Application.Features.MOD_INV;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
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

internal static class StockReceiptHttpSmoke
{
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Fail && data.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action == "MOD_INV_RECEIPT_CONFIRMED")) throw new InvalidOperationException("Synthetic audit failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool, string> check)
    {
        var failure = new FailAudit(); var configured = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure).Options;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); builder.Configuration["Jwt:Secret"] = secret;
        builder.Services.AddScoped(_ => new DentalDbContext(configured)); builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddScoped<IStockReceiptRepository, StockReceiptRepository>(); builder.Services.AddScoped<StockReceiptService>();
        builder.Services.AddScoped<IMedicalSupplyCatalogRepository, MedicalSupplyCatalogRepository>(); builder.Services.AddScoped<MedicalSupplyCatalogService>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>(); builder.Services.AddScoped<IAuditLogger, AuditLogger>(); builder.Services.AddSingleton(new VietnamClock(TimeProvider.System));
        builder.Services.AddControllers().AddApplicationPart(typeof(StockReceiptsController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt => { jwt.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "DentalClinic", ValidateAudience = true, ValidAudience = "DentalClinicClient", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), ClockSkew = TimeSpan.Zero }; jwt.Events = new JwtBearerEvents { OnTokenValidated = AccessTokenVersionValidation.ValidateAsync }; });
        builder.Services.AddAuthorization(x => x.AddApplicationPolicies());
        await using var app = builder.Build(); app.Use(async (c, next) => { try { await next(c); } catch { c.Response.StatusCode = 500; } });
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var tokens = new JwtTokenService(builder.Configuration); await using var db = new DentalDbContext(configured);
        var users = new Dictionary<string, User>();
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Receptionist, RoleCodes.Patient })
        { var u = new User { Role = new Role { RoleCode = role, RoleName = "Stock " + role }, FullName = "Synthetic stock actor " + role, Phone = "095000000" + users.Count, PasswordHash = "synthetic-not-used" }; users[role] = u; db.Users.Add(u); }
        await db.SaveChangesAsync();
        var supply = new MedicalSupply { Code = "VT-TEST", Name = "Vật tư tổng hợp", SearchText = DiagnosisSearch.Normalize("VT-TEST Vật tư tổng hợp ml"), Unit = "ml", MinimumStockThreshold = 5, CreatedByUserId = users[RoleCodes.Admin].UserId };
        var other = new MedicalSupply { Code = "VT-SECOND", Name = "Vật tư thứ hai", Unit = "Cái", CreatedByUserId = users[RoleCodes.Admin].UserId };
        var inactive = new MedicalSupply { Code = "VT-OFF", Name = "Ngừng dùng", Unit = "Cái", IsActive = false, CreatedByUserId = users[RoleCodes.Admin].UserId };
        db.MedicalSupplies.AddRange(supply, other, inactive); await db.SaveChangesAsync();
        void As(string? role) => client.DefaultRequestHeaders.Authorization = role is null ? null : new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(users[role]));
        StockReceiptRequest Input(int qty = 5) => new(Guid.NewGuid(), true, "NCC tổng hợp", "Ghi chú phiếu", [new(supply.Id, qty, supply.Version)]);
        async Task<int> OnHand(int id) => await db.StockBalances.AsNoTracking().Where(x => x.MedicalSupplyId == id).Select(x => x.OnHand).SingleOrDefaultAsync();
        async Task<HttpResponseMessage> Send(StockReceiptRequest input) => await client.PostAsJsonAsync("/api/inventory/receipts", input);
        try
        {
            As(null); using (var r = await client.GetAsync("/api/inventory/stock")) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous stock denied");
            using (var r = await Send(Input())) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous receipt denied");
            foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Receptionist }) { As(role); using var r = await client.GetAsync("/api/inventory/stock"); check(r.StatusCode == HttpStatusCode.Forbidden, role + " stock denied"); using var w = await Send(Input()); check(w.StatusCode == HttpStatusCode.Forbidden, role + " receipt denied"); }
            As(RoleCodes.Dentist); var stock = (await client.GetFromJsonAsync<PagedResult<StockItemDto>>("/api/inventory/stock"))!;
            check(stock.Items.Count == 2 && stock.Items.All(x => x.IsActive && x.OnHand == 0), "Dentist reads active supplies with true empty stock");
            using (var r = await Send(Input())) check(r.StatusCode == HttpStatusCode.Forbidden, "Dentist cannot confirm receipt");
            using (var r = await client.GetAsync("/api/inventory/receipts")) check(r.StatusCode == HttpStatusCode.Forbidden, "Dentist cannot read supplier receipt history");
            As(RoleCodes.Assistant);
            using (var r = await client.GetAsync("/api/master/medical-supplies")) check(r.StatusCode == HttpStatusCode.Forbidden, "Stock permission does not open master catalog API");
            using (var r = await client.GetAsync("/api/inventory/stock")) check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true, "Assistant stock selector protected NoStore");
            var request = Input(); StockReceiptSaved saved;
            using (var r = await Send(request)) { check(r.StatusCode == HttpStatusCode.Created && r.Headers.CacheControl?.NoStore == true, "Assistant explicit receipt 201 NoStore"); saved = (await r.Content.ReadFromJsonAsync<StockReceiptSaved>())!; }
            check(await OnHand(supply.Id) == 5 && saved.Receipt.Code.StartsWith("PN") && saved.Receipt.Lines.Single().Unit == "ml", "Receipt adds integer stock and snapshots unit");
            check(await db.AuditLogs.CountAsync(x => x.Action == "MOD_INV_RECEIPT_CONFIRMED") == 1, "Required stock audit committed once");
            using (var r = await Send(request)) { var repeat = await r.Content.ReadFromJsonAsync<StockReceiptSaved>(); check(r.StatusCode == HttpStatusCode.OK && repeat!.Replayed && repeat.Receipt.Id == saved.Receipt.Id, "Same key and payload returns prior receipt"); }
            check(await OnHand(supply.Id) == 5 && await db.StockReceipts.CountAsync() == 1 && await db.AuditLogs.CountAsync(x => x.Action == "MOD_INV_RECEIPT_CONFIRMED") == 1, "Replay cannot increment stock or audit twice");
            using (var r = await Send(request with { Lines = [new(supply.Id, 6, 1)] })) check(r.StatusCode == HttpStatusCode.Conflict, "Request key cannot change quantity");
            As(RoleCodes.Admin); using (var r = await Send(request)) check(r.StatusCode == HttpStatusCode.Conflict, "Another actor cannot reuse request key");
            using (var r = await Send(Input(2))) check(r.StatusCode == HttpStatusCode.Created, "Admin can confirm separate receipt");
            check(await OnHand(supply.Id) == 7, "Second receipt accumulates stock");
            foreach (var bad in new[] { Input() with { Confirmed = false }, Input(0), Input(-1), Input() with { Lines = [] }, Input() with { Lines = [new(supply.Id, 1, 1), new(supply.Id, 2, 1)] }, Input() with { SupplierName = "" }, Input() with { Lines = null! }, Input() with { Lines = [null!] } })
            { using var r = await Send(bad); check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid receipt rejected before stock writes"); }
            using (var r = await client.PostAsJsonAsync("/api/inventory/receipts", new { requestId = Guid.NewGuid(), confirmed = true, supplierName = "NCC", lines = new[] { new { medicalSupplyId = supply.Id, quantity = 0.5m, expectedSupplyVersion = 1 } } })) check(r.StatusCode == HttpStatusCode.BadRequest, "Fractional JSON quantity rejected, never rounded");
            using (var r = await Send(Input() with { Lines = [new(inactive.Id, 1, 1)] })) check(r.StatusCode == HttpStatusCode.Conflict, "Inactive material rejected");
            using (var r = await Send(Input() with { Lines = [new(supply.Id, 4, 1), new(inactive.Id, 4, 1)] })) check(r.StatusCode == HttpStatusCode.Conflict && await OnHand(supply.Id) == 7, "Invalid later line cannot partially increment earlier stock");
            using (var r = await Send(Input() with { Lines = [new(supply.Id, 1, 999)] })) check(r.StatusCode == HttpStatusCode.Conflict, "Stale catalog version rejected");
            using (var r = await Send(Input() with { Lines = [new(int.MaxValue, 1, 1)] })) check(r.StatusCode == HttpStatusCode.Conflict, "Unknown material rejected");
            using (var r = await Send(Input(int.MaxValue))) check(r.StatusCode == HttpStatusCode.BadRequest && await OnHand(supply.Id) == 7, "Overflow rejected without partial stock update");
            var beforeCount = await db.StockReceipts.CountAsync(); failure.Fail = true;
            using (var r = await Send(Input())) check(r.StatusCode == HttpStatusCode.InternalServerError, "Audit write failure rejects receipt"); failure.Fail = false;
            check(await OnHand(supply.Id) == 7 && await db.StockReceipts.CountAsync() == beforeCount, "Audit failure rolls back receipt, lines and stock");
            var versioned = new MedicalSupplyWriteRequest(supply.Code, supply.Name, "Chai", 5, null, true, supply.Version);
            using (var r = await client.PutAsJsonAsync($"/api/master/medical-supplies/{supply.Id}", versioned)) check(r.StatusCode == HttpStatusCode.BadRequest && (await r.Content.ReadAsStringAsync()).Contains("MST_093"), "Unit change blocked after first stock document");
            using (var r = await client.PutAsJsonAsync($"/api/master/medical-supplies/{other.Id}", new MedicalSupplyWriteRequest(other.Code, other.Name, "Gram", 0, null, true, other.Version))) check(r.StatusCode == HttpStatusCode.OK, "Unit may change before any stock document");
            db.ChangeTracker.Clear(); var fresh = await db.MedicalSupplies.AsNoTracking().SingleAsync(x => x.Id == other.Id);
            using (var r = await Send(Input() with { Lines = [new(other.Id, 3, fresh.Version)] })) check(r.StatusCode == HttpStatusCode.Created, "Receipt snapshots freshly selected catalog unit");
            check(await OnHand(other.Id) == 3, "Other material stock independent");
            As(RoleCodes.Assistant);
            var raceA = Input(3); var raceB = Input(4);
            var responses = await Task.WhenAll(Send(raceA), Send(raceB));
            check(responses.All(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict) && responses.Any(r => r.StatusCode == HttpStatusCode.Created), "Concurrent receipts commit or return conflict safely");
            for (var i = 0; i < responses.Length; i++) { if (responses[i].StatusCode == HttpStatusCode.Conflict) { using var retry = await Send(i == 0 ? raceA : raceB); check(retry.StatusCode == HttpStatusCode.Created, "Concurrent receipt retry succeeds with same key"); } responses[i].Dispose(); }
            check(await OnHand(supply.Id) == 14, "Concurrent receipts never lose stock increments");
            var duplicate = Input(2); var repeated = await Task.WhenAll(Send(duplicate), Send(duplicate)); foreach (var r in repeated) r.Dispose();
            using (var r = await Send(duplicate)) check(r.StatusCode == HttpStatusCode.OK && (await r.Content.ReadFromJsonAsync<StockReceiptSaved>())!.Replayed, "Concurrent same key resolves by replay");
            check(await OnHand(supply.Id) == 16 && await db.StockReceipts.CountAsync(x => x.RequestId == duplicate.RequestId) == 1, "Unique request key prevents concurrent duplicate stock");
            using (var r = await client.GetAsync($"/api/inventory/receipts/{saved.Receipt.Id}")) { var detail = await r.Content.ReadFromJsonAsync<StockReceiptDto>(); check(r.StatusCode == HttpStatusCode.OK && detail!.ConfirmedByUserId == users[RoleCodes.Assistant].UserId && detail.Lines.Single().Quantity == 5, "Receipt detail preserves original confirmer and quantity"); }
            using (var r = await client.GetAsync("/api/inventory/receipts")) check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true, "Writer receipt history protected NoStore");
            using (var r = await client.GetAsync("/api/inventory/stock?keyword=vat%20tu&page=1")) check(r.StatusCode == HttpStatusCode.OK && (await r.Content.ReadFromJsonAsync<PagedResult<StockItemDto>>())!.Items.Any(x => x.MedicalSupplyId == supply.Id), "Stock search uses normalized Vietnamese catalog");
            using (var r = await client.GetAsync("/api/inventory/stock?page=0")) check(r.StatusCode == HttpStatusCode.BadRequest, "Stock paging bounded");
            using (var r = await client.GetAsync("/api/inventory/receipts/-1")) check(r.StatusCode == HttpStatusCode.BadRequest, "Receipt ID validated");
            var persisted = await db.StockReceipts.Include(x => x.Lines).SingleAsync(x => x.Id == saved.Receipt.Id); persisted.SupplierName = "Changed";
            try { await db.SaveChangesAsync(); check(false, "Receipt must be immutable"); } catch (InvalidOperationException) { check(true, "Confirmed receipt cannot be edited"); } db.ChangeTracker.Clear();
            var line = await db.StockReceiptLines.FirstAsync(); line.Quantity++;
            try { await db.SaveChangesAsync(); check(false, "Receipt line must be immutable"); } catch (InvalidOperationException) { check(true, "Confirmed receipt line cannot be edited"); } db.ChangeTracker.Clear();
            db.StockReceiptLines.Add(new StockReceiptLine { StockReceiptId = saved.Receipt.Id, MedicalSupplyId = other.Id, SupplyCode = other.Code, SupplyName = other.Name, Unit = other.Unit, Quantity = 1 });
            try { await db.SaveChangesAsync(); check(false, "Old receipt cannot gain a line"); } catch (InvalidOperationException) { check(true, "Confirmed receipt cannot gain lines"); } db.ChangeTracker.Clear();
            var actor = await db.Users.SingleAsync(x => x.UserId == users[RoleCodes.Assistant].UserId); actor.IsActive = false; await db.SaveChangesAsync();
            using (var r = await Send(Input())) check(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized, "Inactive actor with old JWT cannot confirm");
            using (var r = await client.GetAsync("/api/inventory/stock")) check(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized, "Inactive actor with old JWT cannot read stock");
        }
        finally { await app.StopAsync(); }
    }
}
