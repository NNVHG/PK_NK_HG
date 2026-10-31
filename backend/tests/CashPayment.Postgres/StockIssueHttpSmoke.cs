using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Application.Features.MOD_INV;
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

internal static class StockIssueHttpSmoke
{
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Fail && data.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action == "MOD_INV_ISSUE_CONFIRMED")) throw new InvalidOperationException("Synthetic audit failure");
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
        builder.Services.AddScoped<IStockIssueRepository, StockIssueRepository>(); builder.Services.AddScoped<StockIssueService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(StockIssuesController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt => { jwt.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "DentalClinic", ValidateAudience = true, ValidAudience = "DentalClinicClient", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), ClockSkew = TimeSpan.Zero }; jwt.Events = new JwtBearerEvents { OnTokenValidated = AccessTokenVersionValidation.ValidateAsync }; });
        builder.Services.AddAuthorization(x => x.AddApplicationPolicies());
        await using var app = builder.Build(); app.Use(async (c, next) => { try { await next(c); } catch { c.Response.StatusCode = 500; } });
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var tokens = new JwtTokenService(builder.Configuration); await using var db = new DentalDbContext(configured);
        var users = new Dictionary<string, User>();
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Receptionist, RoleCodes.Patient })
        { var u = new User { Role = new Role { RoleCode = role, RoleName = "Issue " + role }, FullName = "Synthetic issue actor " + role, Phone = "094000000" + users.Count, PasswordHash = "synthetic-not-used" }; users[role] = u; db.Users.Add(u); }
        await db.SaveChangesAsync();
        var patient = new Patient { FullName = "Synthetic stock issue patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0940000008" }; db.Patients.Add(patient); await db.SaveChangesAsync();
        async Task<Visit> Visit(string state)
        { var v = new Visit { PatientId = patient.PatientId, Status = state, CreatedByUserId = users[RoleCodes.Admin].UserId, EndedAt = DateTime.UtcNow, IsLocked = state == VisitStatuses.Completed }; db.Visits.Add(v); await db.SaveChangesAsync(); return v; }
        var complete = await Visit(VisitStatuses.Completed); var started = await Visit(VisitStatuses.InProgress); var cancelled = await Visit(VisitStatuses.Cancelled); var correcting = await Visit(VisitStatuses.Correcting); var created = await Visit(VisitStatuses.Created);
        var supply = new MedicalSupply { Code = "VT-ISSUE", Name = "Vật tư tổng hợp", SearchText = "vat tu", Unit = "ml", MinimumStockThreshold = 5, CreatedByUserId = users[RoleCodes.Admin].UserId };
        var empty = new MedicalSupply { Code = "VT-EMPTY", Name = "Chưa nhập", Unit = "Cái", CreatedByUserId = users[RoleCodes.Admin].UserId };
        var inactive = new MedicalSupply { Code = "VT-OFF", Name = "Ngừng dùng", Unit = "Cái", IsActive = false, CreatedByUserId = users[RoleCodes.Admin].UserId };
        db.MedicalSupplies.AddRange(supply, empty, inactive); await db.SaveChangesAsync();
        void As(string? role) => client.DefaultRequestHeaders.Authorization = role is null ? null : new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(users[role]));
        StockIssueRequest Input(int qty = 3) => new(Guid.NewGuid(), true, complete.VisitId, "Tiêu hao thực tế", [new(supply.Id, qty, 1)]);
        async Task<int> OnHand() => await db.StockBalances.AsNoTracking().Where(x => x.MedicalSupplyId == supply.Id).Select(x => x.OnHand).SingleAsync();
        async Task<HttpResponseMessage> Send(StockIssueRequest input) => await client.PostAsJsonAsync("/api/inventory/issues", input);
        try
        {
            As(RoleCodes.Admin); using (var r = await client.PostAsJsonAsync("/api/inventory/receipts", new StockReceiptRequest(Guid.NewGuid(), true, "Synthetic NCC", null, [new(supply.Id, 20, 1)]))) check(r.StatusCode == HttpStatusCode.Created, "Seed stock through authorized receipt API");
            check(await OnHand() == 20 && await db.StockIssues.CountAsync() == 0, "Completed visit does not automatically deduct inventory");
            As(null); using (var r = await Send(Input())) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous cannot confirm issue");
            using (var r = await client.GetAsync("/api/inventory/issues/targets")) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous cannot read visit targets");
            foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Receptionist, RoleCodes.Dentist })
            { As(role); using var r = await Send(Input()); check(r.StatusCode == HttpStatusCode.Forbidden, role + " issue denied"); using var q = await client.GetAsync("/api/inventory/issues/targets"); check(q.StatusCode == HttpStatusCode.Forbidden, role + " targets denied"); using var h = await client.GetAsync($"/api/inventory/issues?visitId={complete.VisitId}"); check(h.StatusCode == HttpStatusCode.Forbidden, role + " issue history denied"); }
            As(RoleCodes.Assistant);
            using (var r = await client.GetAsync("/api/inventory/issues/targets")) { var rows = (await r.Content.ReadFromJsonAsync<StockIssueTargets>())!; check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true && rows.Items.Single().VisitId == complete.VisitId, "Assistant sees only completed visit targets, NoStore"); var body = await r.Content.ReadAsStringAsync(); check(!body.Contains("phone", StringComparison.OrdinalIgnoreCase) && !body.Contains("diagnosis", StringComparison.OrdinalIgnoreCase) && !body.Contains("citizenId", StringComparison.OrdinalIgnoreCase), "Target DTO excludes contacts, diagnosis and identity number"); }
            foreach (var id in new[] { started.VisitId, cancelled.VisitId, correcting.VisitId, created.VisitId, int.MaxValue }) { using var r = await Send(Input() with { VisitId = id }); check(r.StatusCode == HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("INV_VISIT"), "Non-completed or missing visit cannot issue stock"); }
            using (var r = await Send(Input() with { Confirmed = false })) check(r.StatusCode == HttpStatusCode.BadRequest && await OnHand() == 20, "No confirmation means no deduction");
            var request = Input(); StockIssueSaved saved;
            using (var r = await Send(request)) { check(r.StatusCode == HttpStatusCode.Created && r.Headers.CacheControl?.NoStore == true, "Assistant confirms stock issue 201 NoStore"); saved = (await r.Content.ReadFromJsonAsync<StockIssueSaved>())!; }
            check(saved.Issue.Code.StartsWith("PX") && saved.Issue.Lines.Single().Unit == "ml" && await OnHand() == 17, "Issue snapshots unit and deducts integer quantity");
            check(await db.AuditLogs.CountAsync(x => x.Action == "MOD_INV_ISSUE_CONFIRMED") == 1, "Exactly one required issue audit");
            using (var r = await Send(request)) check(r.StatusCode == HttpStatusCode.OK && (await r.Content.ReadFromJsonAsync<StockIssueSaved>())!.Replayed, "Same request replays prior issue");
            check(await OnHand() == 17 && await db.StockIssues.CountAsync() == 1, "Replay never deducts twice");
            using (var r = await Send(request with { Lines = [new(supply.Id, 4, 1)] })) check(r.StatusCode == HttpStatusCode.Conflict, "Key reuse with changed payload denied");
            As(RoleCodes.Admin); using (var r = await Send(request)) check(r.StatusCode == HttpStatusCode.Conflict, "Another actor cannot reuse key");
            using (var r = await Send(Input(2))) check(r.StatusCode == HttpStatusCode.Created && await OnHand() == 15, "Admin confirms separate supplemental actual quantity");
            foreach (var bad in new[] { Input(0), Input(-1), Input() with { Lines = [] }, Input() with { Lines = null! }, Input() with { Lines = [null!] }, Input() with { Notes = new string('x', 501) }, Input() with { Lines = [new(supply.Id, 1, 1), new(supply.Id, 1, 1)] } }) { using var r = await Send(bad); check(r.StatusCode == HttpStatusCode.BadRequest, "Malformed quantity/lines/notes denied"); }
            using (var r = await client.PostAsJsonAsync("/api/inventory/issues", new { requestId = Guid.NewGuid(), confirmed = true, visitId = complete.VisitId, lines = new[] { new { medicalSupplyId = supply.Id, quantity = 0.5m, expectedSupplyVersion = 1 } } })) check(r.StatusCode == HttpStatusCode.BadRequest, "Fractional actual quantity rejected, never rounded");
            foreach (var bad in new[] { Input() with { Lines = [new(inactive.Id, 1, 1)] }, Input() with { Lines = [new(supply.Id, 1, 999)] }, Input() with { Lines = [new(int.MaxValue, 1, 1)] } }) { using var r = await Send(bad); check(r.StatusCode == HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("INV_STALE"), "Invalid/inactive/stale material denied"); }
            using (var r = await Send(Input(16))) check(r.StatusCode == HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("INV_INSUFFICIENT"), "Insufficient stock rejects issue");
            using (var r = await Send(Input() with { Lines = [new(supply.Id, 1, 1), new(empty.Id, 1, 1)] })) check(r.StatusCode == HttpStatusCode.Conflict && await OnHand() == 15, "Later missing balance rolls back all earlier line effects");
            var count = await db.StockIssues.CountAsync(); failure.Fail = true;
            using (var r = await Send(Input())) check(r.StatusCode == HttpStatusCode.InternalServerError, "Audit failure rejects issue"); failure.Fail = false;
            check(await OnHand() == 15 && await db.StockIssues.CountAsync() == count && await db.AuditLogs.CountAsync(x => x.Action == "MOD_INV_ISSUE_CONFIRMED") == 2, "Audit failure rolls back stock, header and lines");
            var races = await Task.WhenAll(Send(Input(10)), Send(Input(10))); check(races.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && races.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1 && await OnHand() == 5, "Concurrent distinct issues never overspend stock"); foreach (var r in races) r.Dispose();
            var duplicate = Input(2); var doubles = await Task.WhenAll(Send(duplicate), Send(duplicate)); foreach (var r in doubles) r.Dispose();
            using (var r = await Send(duplicate)) check(r.StatusCode == HttpStatusCode.OK && (await r.Content.ReadFromJsonAsync<StockIssueSaved>())!.Replayed, "Concurrent same key resolves by replay");
            check(await OnHand() == 3 && await db.StockIssues.CountAsync(x => x.RequestId == duplicate.RequestId) == 1, "Same-key race deducts once");
            var receiptRequest = new StockReceiptRequest(Guid.NewGuid(), true, "Synthetic NCC", null, [new(supply.Id, 5, 1)]); var issueRequest = Input(3);
            var receiptTask = client.PostAsJsonAsync("/api/inventory/receipts", receiptRequest); var issueTask = Send(issueRequest); await Task.WhenAll(receiptTask, issueTask);
            using (var r = await receiptTask) { if (r.StatusCode == HttpStatusCode.Conflict) { using var retry = await client.PostAsJsonAsync("/api/inventory/receipts", receiptRequest); check(retry.StatusCode == HttpStatusCode.Created, "Receipt retries after concurrent issue"); } else check(r.StatusCode == HttpStatusCode.Created, "Receipt commits alongside issue"); }
            using (var r = await issueTask) { if (r.StatusCode == HttpStatusCode.Conflict) { using var retry = await Send(issueRequest); check(retry.StatusCode == HttpStatusCode.Created, "Issue retries after concurrent receipt"); } else check(r.StatusCode == HttpStatusCode.Created, "Issue commits alongside receipt"); }
            check(await OnHand() == 5, "Shared balance correct after concurrent receipt and issue");
            using (var r = await client.GetAsync($"/api/inventory/issues?visitId={complete.VisitId}")) { var rows = (await r.Content.ReadFromJsonAsync<StockIssuePage>())!; check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true && rows.Items.Any(x => x.Id == saved.Issue.Id && x.ConfirmedByUserId == users[RoleCodes.Assistant].UserId && x.Lines.Single().Quantity == 3), "History preserves original actor and quantity, NoStore"); }
            using (var r = await client.GetAsync("/api/inventory/issues?visitId=2147483647")) check(r.StatusCode == HttpStatusCode.NotFound, "Unknown visit history returns404");
            using (var r = await client.GetAsync("/api/inventory/issues?visitId=0")) check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid visit history filter denied");
            var persisted = await db.StockIssues.Include(x => x.Lines).SingleAsync(x => x.Id == saved.Issue.Id); persisted.Notes = "Changed";
            try { await db.SaveChangesAsync(); check(false, "Issue immutable"); } catch (InvalidOperationException) { check(true, "Issue header cannot be edited"); } db.ChangeTracker.Clear();
            var line = await db.StockIssueLines.FirstAsync(); db.StockIssueLines.Remove(line);
            try { await db.SaveChangesAsync(); check(false, "Issue line immutable"); } catch (InvalidOperationException) { check(true, "Issue line cannot be deleted"); } db.ChangeTracker.Clear();
            db.StockIssueLines.Add(new StockIssueLine { StockIssueId = saved.Issue.Id, MedicalSupplyId = empty.Id, SupplyCode = empty.Code, SupplyName = empty.Name, Unit = empty.Unit, Quantity = 1 });
            try { await db.SaveChangesAsync(); check(false, "Issue cannot gain lines"); } catch (InvalidOperationException) { check(true, "Issue cannot gain lines after confirmation"); } db.ChangeTracker.Clear();
            var unchanged = await db.Visits.AsNoTracking().SingleAsync(x => x.VisitId == complete.VisitId); check(unchanged.Status == VisitStatuses.Completed && unchanged.IsLocked && unchanged.Diagnosis == null && !await db.Invoices.AnyAsync(), "Stock issue does not reopen visit or create/change financial records");
            var v = await db.Visits.SingleAsync(x => x.VisitId == complete.VisitId); v.Status = VisitStatuses.Correcting; await db.SaveChangesAsync();
            using (var r = await Send(request with { Notes = request.Notes })) check(r.StatusCode == HttpStatusCode.Conflict, "Different actor cannot replay even after visit changes");
            As(RoleCodes.Assistant); using (var r = await Send(request)) check(r.StatusCode == HttpStatusCode.OK && await OnHand() == 5, "Original replay survives visit reopen without new deduction");
            using (var r = await Send(Input())) check(r.StatusCode == HttpStatusCode.Conflict, "Reopened correcting visit cannot issue new stock");
            using (var r = await client.GetAsync($"/api/inventory/issues?visitId={complete.VisitId}")) check(r.StatusCode == HttpStatusCode.OK, "Stock history remains readable after visit lifecycle changes");
            var actor = await db.Users.SingleAsync(x => x.UserId == users[RoleCodes.Assistant].UserId); actor.IsActive = false; await db.SaveChangesAsync();
            using (var r = await Send(Input())) check(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized, "Inactive actor with old JWT cannot issue");
            using (var r = await client.GetAsync("/api/inventory/issues/targets")) check(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized, "Inactive actor with old JWT cannot read targets");
        }
        finally { await app.StopAsync(); }
    }
}
