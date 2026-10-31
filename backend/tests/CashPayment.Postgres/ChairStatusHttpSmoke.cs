using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Application.Common;
using Dental.Application.Features.MOD_ASS.DTOs;
using Dental.Application.Features.MOD_ASS.Services;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
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

internal static class ChairStatusHttpSmoke
{
    private sealed class ChunkedJson(string text) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(Encoding.UTF8.GetBytes(text)).AsTask();
    }
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Fail && data.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action == "MOD_ASS_CHAIR_STATUS_CHANGED"))
                throw new InvalidOperationException("Synthetic audit failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
    private sealed class RaceGate : DbCommandInterceptor
    {
        private TaskCompletionSource? gate;
        private int arrivals;
        public void Arm() { arrivals = 0; gate = new(TaskCreationOptions.RunContinuationsAsynchronously); }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data, DbDataReader result, CancellationToken ct = default)
        {
            var current = gate;
            if (current is not null && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("FROM \"DentalChairs\"", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref arrivals) == 2) { gate = null; current.TrySetResult(); }
                await current.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
            return result;
        }
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool, string> check)
    {
        await using var ownership = new DentalDbContext(options);
        if (!ownership.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Owned database required");
        var failure = new FailAudit(); var race = new RaceGate();
        var configured = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure, race).Options;
        var clock = new VietnamClock(TimeProvider.System);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); builder.Configuration["Jwt:Secret"] = secret;
        builder.Services.AddScoped(_ => new DentalDbContext(configured)); builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddScoped<IChairStatusRepository, ChairStatusRepository>(); builder.Services.AddScoped<ChairStatusService>();
        builder.Services.AddScoped<IDentalChairRepository, DentalChairRepository>(); builder.Services.AddScoped<DentalChairService>();
        builder.Services.AddScoped<IStaffWorkspaceRepository, StaffWorkspaceRepository>(); builder.Services.AddScoped<StaffWorkspaceService>();
        builder.Services.AddScoped<IUserRepository, UserRepository>(); builder.Services.AddScoped<IVisitRepository, VisitRepository>(); builder.Services.AddScoped<IQueueRepository, QueueRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>(); builder.Services.AddScoped<IAuditLogger, AuditLogger>(); builder.Services.AddSingleton(clock);
        builder.Services.AddControllers().AddApplicationPart(typeof(ChairStatusController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
        {
            jwt.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "DentalClinic", ValidateAudience = true, ValidAudience = "DentalClinicClient", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), ClockSkew = TimeSpan.Zero };
            jwt.Events = new JwtBearerEvents { OnTokenValidated = AccessTokenVersionValidation.ValidateAsync };
        });
        builder.Services.AddAuthorization(x => x.AddApplicationPolicies());
        await using var app = builder.Build(); app.Use(async (c, next) => { try { await next(c); } catch { c.Response.StatusCode = 500; } });
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var tokens = new JwtTokenService(builder.Configuration); await using var db = new DentalDbContext(configured);
        var users = new Dictionary<string, User>();
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Receptionist, RoleCodes.Patient })
        {
            var user = new User { Role = new Role { RoleCode = role, RoleName = role }, FullName = "Synthetic chair actor", Phone = "095000000" + users.Count, PasswordHash = "synthetic-not-used" };
            users[role] = user; db.Users.Add(user);
        }
        await db.SaveChangesAsync(); var adminId = users[RoleCodes.Admin].UserId; var dentistId = users[RoleCodes.Dentist].UserId;
        void As(string? role) => client.DefaultRequestHeaders.Authorization = role is null ? null : new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(users[role]));
        const string endpoint = "/api/chair-status";
        Task<HttpResponseMessage> Change(int id, int status = 3, int version = 1, bool confirmed = true)
            => client.PutAsJsonAsync(endpoint + "/" + id, new ChangeChairStatusRequest(status, version, confirmed));
        Task<DentalChair> Read(int id) => db.DentalChairs.AsNoTracking().SingleAsync(c => c.Id == id);
        Task<int> AuditCount() => db.AuditLogs.CountAsync(a => a.Action == "MOD_ASS_CHAIR_STATUS_CHANGED");
        async Task<DentalChair> NewChair(string code, DentalChairStatus status = DentalChairStatus.Available)
        {
            var value = new DentalChair { ChairNumber = code, RoomName = "Synthetic room", Description = "Keep catalog text", Status = status, IsActive = status != DentalChairStatus.Inactive, CreatedByUserId = adminId };
            db.DentalChairs.Add(value); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return value;
        }
        try
        {
            As(null);
            using (var r = await client.GetAsync(endpoint)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous read401");
            using (var r = await Change(1)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous mutation401");
            foreach (var role in new[] { RoleCodes.Dentist, RoleCodes.Patient, RoleCodes.Receptionist })
            {
                As(role); using var read = await client.GetAsync(endpoint); using var write = await Change(1);
                check(read.StatusCode == HttpStatusCode.Forbidden, role + " operational read403"); check(write.StatusCode == HttpStatusCode.Forbidden, role + " operational write403");
            }
            As(RoleCodes.Assistant);
            using (var r = await client.GetAsync(endpoint))
            { var result = (await r.Content.ReadFromJsonAsync<PagedResult<ChairStatusResponse>>())!; check(r.StatusCode == HttpStatusCode.OK && result.Items.Count == 0 && result.TotalCount == 0, "Assistant actual empty page"); check(r.Headers.CacheControl?.NoStore == true, "Read NoStore"); }
            var chair = await NewChair("GHE-01"); var off = await NewChair("GHE-OFF", DentalChairStatus.Inactive);
            using (var r = await client.GetAsync(endpoint + "?keyword=GHE-01&status=1&pageSize=1"))
            {
                var result = (await r.Content.ReadFromJsonAsync<PagedResult<ChairStatusResponse>>())!;
                check(r.StatusCode == HttpStatusCode.OK && result.Items.Single().Id == chair.Id && result.TotalCount == 1, "Bounded search/status projection");
                var json = await r.Content.ReadAsStringAsync(); check(!json.Contains("patient", StringComparison.OrdinalIgnoreCase) && !json.Contains("description", StringComparison.OrdinalIgnoreCase) && !json.Contains("phone", StringComparison.OrdinalIgnoreCase), "DTO excludes patient and catalog description");
            }
            foreach (var query in new[] { "page=0", "page=1000001", "pageSize=101", "status=5", "keyword=" + new string('x', 101) })
            { using var r = await client.GetAsync(endpoint + "?" + query); check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid filter denied " + query.Split('=')[0]); }
            using (var r = await Change(chair.Id, 2)) check(r.StatusCode == HttpStatusCode.BadRequest, "Manual Occupied rejected");
            using (var r = await Change(chair.Id, 5)) check(r.StatusCode == HttpStatusCode.BadRequest, "Unknown state rejected");
            using (var r = await Change(chair.Id, confirmed: false)) check(r.StatusCode == HttpStatusCode.BadRequest, "Explicit confirmation required");
            using (var r = await Change(chair.Id, version: 0)) check(r.StatusCode == HttpStatusCode.BadRequest, "Positive version required");
            using (var r = await Change(int.MaxValue)) check(r.StatusCode == HttpStatusCode.NotFound, "Unknown chair404");
            using (var r = await Change(chair.Id, version: 2)) check(r.StatusCode == HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("ASS_061"), "Stale version409");
            using (var r = await client.PutAsync(endpoint + "/" + chair.Id, new StringContent("{\"status\":3,\"expectedVersion\":1,\"confirmed\":true,\"extra\":\"" + new string('x', 5000) + "\"}", Encoding.UTF8, "application/json")))
                check(r.StatusCode == HttpStatusCode.RequestEntityTooLarge && r.Headers.CacheControl?.NoStore == true, "Mutation payload bounded4KiB actual=" + (int)r.StatusCode);
            using (var body = new ChunkedJson("{\"status\":3,\"expectedVersion\":1,\"confirmed\":true,\"extra\":\"" + new string('x', 5000) + "\"}"))
            {
                body.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                using var r = await client.PutAsync(endpoint + "/" + chair.Id, body);
                check(r.StatusCode == HttpStatusCode.RequestEntityTooLarge && r.Headers.CacheControl?.NoStore == true, "Chunked body cannot bypass4KiB NoStore");
            }
            check((await Read(chair.Id)).Version == 1 && await AuditCount() == 0, "Oversize requests do not mutate or audit");
            using (var r = await Change(chair.Id)) check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true, "Assistant maintenance200 NoStore");
            var current = await Read(chair.Id); check(current.Status == DentalChairStatus.Maintenance && current.IsActive && current.Version == 2 && current.UpdatedByUserId == users[RoleCodes.Assistant].UserId && current.UpdatedAt.HasValue, "Status/version/real updater persisted");
            check(await AuditCount() == 1, "One required audit for change");
            using (var r = await Change(chair.Id, 3, 2)) check(r.StatusCode == HttpStatusCode.OK && await AuditCount() == 1 && (await Read(chair.Id)).Version == 2, "No-op does not invent history");
            using (var r = await Change(chair.Id, 4, 2)) check(r.StatusCode == HttpStatusCode.OK && !(await Read(chair.Id)).IsActive, "Assistant can pause unassigned chair");
            using (var r = await Change(off.Id, 1)) check(r.StatusCode == HttpStatusCode.OK && (await Read(off.Id)).IsActive, "Assistant restores inactive chair to Ready");
            using (var r = await client.PostAsJsonAsync("/api/master/chairs", new CreateDentalChairRequest("NEW", "Room", 1, null, true))) check(r.StatusCode == HttpStatusCode.Forbidden, "Assistant cannot create catalog");
            using (var r = await client.PutAsJsonAsync("/api/master/chairs/" + chair.Id, new UpdateDentalChairRequest("Changed", 1, "Changed", 3, true))) check(r.StatusCode == HttpStatusCode.Forbidden, "Assistant cannot edit catalog");
            using (var r = await client.PutAsJsonAsync(endpoint + "/" + chair.Id, new { status = 1, expectedVersion = 3, confirmed = true, chairNumber = "ATTACK", roomName = "Changed", description = "Changed", isActive = false })) check(r.StatusCode == HttpStatusCode.OK, "Status operation ignores extra catalog fields");
            current = await Read(chair.Id); check(current.ChairNumber == "GHE-01" && current.RoomName == "Synthetic room" && current.Description == "Keep catalog text" && current.CreatedByUserId == adminId && current.IsActive, "Immutable catalog fields retained");

            // A dentist's own workspace is released using the existing authorized flow.
            As(RoleCodes.Dentist);
            using (var r = await client.PutAsJsonAsync("/api/workspaces/" + dentistId, new ChangeWorkspaceRequest(chair.Id, null, 0, true))) check(r.StatusCode == HttpStatusCode.OK, "Dentist selects Ready chair");
            As(RoleCodes.Assistant);
            using (var r = await Change(chair.Id, 3, 4)) check(r.StatusCode == HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("ASS_063"), "Assigned chair without visit rejects maintenance Q331a");
            using (var r = await Change(chair.Id, 4, 4)) check(r.StatusCode == HttpStatusCode.Conflict, "Assigned chair rejects pause");
            using (var r = await client.GetAsync(endpoint + "?keyword=GHE-01")) check((await r.Content.ReadFromJsonAsync<PagedResult<ChairStatusResponse>>())!.Items.Single().IsAssigned, "List marks assigned chair without patient/staff identity");
            As(RoleCodes.Admin); using (var r = await Change(chair.Id, 3, 4)) check(r.StatusCode == HttpStatusCode.Conflict, "Admin also cannot bypass assignment");
            As(RoleCodes.Dentist); using (var r = await client.PutAsJsonAsync("/api/workspaces/" + dentistId, new ChangeWorkspaceRequest(null, null, 1, true))) check(r.StatusCode == HttpStatusCode.OK, "Dentist clears workspace first");
            As(RoleCodes.Assistant); using (var r = await Change(chair.Id, 3, 4)) check(r.StatusCode == HttpStatusCode.OK && !await db.StaffWorkspaces.AnyAsync(w => w.ChairId == chair.Id), "Maintenance succeeds after workspace release");
            var occupied = await NewChair("GHE-BUSY", DentalChairStatus.Occupied);
            using (var r = await Change(occupied.Id, 1)) check(r.StatusCode == HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("ASS_062"), "Occupied flag cannot be released manually");
            var patient = new Patient { FullName = "Synthetic chair patient", Phone = "0950000008", DateOfBirth = new DateOnly(1990, 1, 1) }; db.Patients.Add(patient); await db.SaveChangesAsync();
            var inconsistent = await NewChair("GHE-LEGACY"); var visit = new Visit { PatientId = patient.PatientId, Status = VisitStatuses.InProgress, ChairId = inconsistent.Id, CreatedByUserId = adminId };
            db.Visits.Add(visit); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            using (var r = await Change(inconsistent.Id)) check(r.StatusCode == HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("ASS_062"), "Actual InProgress blocks even with Available flag");
            using (var r = await client.GetAsync(endpoint + "?keyword=GHE-LEGACY")) check((await r.Content.ReadFromJsonAsync<PagedResult<ChairStatusResponse>>())!.Items.Single().HasActiveVisit, "List exposes active-visit guard without clinical data");
            var correcting = await db.Visits.SingleAsync(v => v.VisitId == visit.VisitId); correcting.Status = VisitStatuses.Correcting; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            using (var r = await Change(inconsistent.Id)) check(r.StatusCode == HttpStatusCode.OK, "Correcting does not occupy historical chair DL263");

            var auditBefore = await AuditCount(); var rollback = await NewChair("GHE-ROLLBACK"); failure.Fail = true;
            using (var r = await Change(rollback.Id)) check(r.StatusCode == HttpStatusCode.InternalServerError, "Audit failure surfaces instead of false success");
            failure.Fail = false; check((await Read(rollback.Id)).Version == 1 && (await Read(rollback.Id)).Status == DentalChairStatus.Available && await AuditCount() == auditBefore, "Audit failure rolls back state/version/history atomically");
            var competing = await NewChair("GHE-RACE"); race.Arm();
            var changes = await Task.WhenAll(Change(competing.Id, 3), Change(competing.Id, 4));
            check(changes.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && changes.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "Concurrent status confirmations have one winner and one409"); foreach (var r in changes) r.Dispose();
            check((await Read(competing.Id)).Version == 2 && await AuditCount() == auditBefore + 1, "Concurrent loser writes no state or audit");

            // Serializable protects a workspace selection racing against maintenance.
            var selecting = await NewChair("GHE-SELECT-RACE"); As(RoleCodes.Admin); race.Arm();
            var selectingTask = client.PutAsJsonAsync("/api/workspaces/" + dentistId, new ChangeWorkspaceRequest(selecting.Id, null, 2, true));
            var maintainingTask = Change(selecting.Id);
            var results = await Task.WhenAll(selectingTask, maintainingTask);
            check(results.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && results.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "Workspace selection versus maintenance has exactly one winner"); foreach (var r in results) r.Dispose();
            var selectedByDentist = await db.StaffWorkspaces.AsNoTracking().AnyAsync(w => w.ChairId == selecting.Id);
            check(!selectedByDentist || (await Read(selecting.Id)).Status == DentalChairStatus.Available, "No assigned-maintenance combination after race");
            check((await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == visit.VisitId)).Status == VisitStatuses.Correcting && !await db.Invoices.AnyAsync(), "Manual statuses do not modify visit or finance");
            var assistant = await db.Users.SingleAsync(u => u.UserId == users[RoleCodes.Assistant].UserId); As(RoleCodes.Assistant); assistant.IsActive = false; await db.SaveChangesAsync();
            using (var r = await client.GetAsync(endpoint)) check(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized, "Inactive actor old JWT cannot read");
            using (var r = await Change(rollback.Id)) check(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized, "Inactive actor old JWT cannot mutate");
            assistant.IsActive = true; assistant.RoleId = users[RoleCodes.Dentist].RoleId; await db.SaveChangesAsync();
            using (var r = await client.GetAsync(endpoint)) check(r.StatusCode == HttpStatusCode.Forbidden, "Changed DB role old JWT cannot read");
            using (var r = await Change(rollback.Id)) check(r.StatusCode == HttpStatusCode.Forbidden, "Changed DB role old JWT cannot mutate");
            check(!await db.AuditLogs.AnyAsync(a => a.Action == "MOD_ASS_CHAIR_STATUS_CHANGED" && (a.Detail!.Contains("Synthetic chair patient") || a.Detail.Contains("0950000008"))), "Status audit carries no patient data");
        }
        finally { await app.StopAsync(); }
    }
}
