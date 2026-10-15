using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

internal static class DentalChairHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Owned database required");
        const string endpoint = "/api/master/chairs";
        var body = new CreateDentalChairRequest("ghe-01", "Phòng 3", 3, "Synthetic chair", true);
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(endpoint)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Chair read anonymous401");
        using (var r = await client.PostAsJsonAsync(endpoint, body)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Chair write anonymous401");
        foreach (var role in new[] { RoleCodes.Receptionist, RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role);
            using (var r = await client.GetAsync(endpoint)) check(r.StatusCode == (role is RoleCodes.Dentist or RoleCodes.Assistant ? HttpStatusCode.OK : HttpStatusCode.Forbidden), $"Chair read matrix {role}");
            using (var r = await client.PostAsJsonAsync(endpoint, body)) check(r.StatusCode == HttpStatusCode.Forbidden, $"Chair create {role}403");
            using (var r = await client.PutAsJsonAsync(endpoint + "/1", new UpdateDentalChairRequest("P", 1, null, 1, true))) check(r.StatusCode == HttpStatusCode.Forbidden, $"Chair update {role}403");
        }
        authenticate(RoleCodes.Admin);
        using (var r = await client.PostAsJsonAsync(endpoint, body with { Confirmed = false })) check(r.StatusCode == HttpStatusCode.BadRequest, "Chair create confirmation400");
        using (var r = await client.PostAsJsonAsync(endpoint, body with { Status = 2 })) check(r.StatusCode == HttpStatusCode.BadRequest, "Chair cannot create Occupied400");
        using (var r = await client.PostAsJsonAsync(endpoint, body with { RoomName = " " })) check(r.StatusCode == HttpStatusCode.BadRequest, "Chair room required400");
        DentalChairResponse created;
        using (var r = await client.PostAsJsonAsync(endpoint, body))
        { created = (await r.Content.ReadFromJsonAsync<DentalChairResponse>())!; check(r.StatusCode == HttpStatusCode.Created && created.Id > 0 && created.ChairNumber == "GHE-01" && created.Status == 3 && created.IsActive && created.Version == 1, "Admin creates normalized maintenance chair201"); }
        check(await db.DentalChairs.AnyAsync(c => c.Id == created.Id && c.CreatedByUserId == actorId) &&
            await db.AuditLogs.CountAsync(a => a.EntityType == "DentalChair" && a.EntityId == created.Id) == 1, "Chair and required audit committed together");
        using (var r = await client.PostAsJsonAsync(endpoint, body with { ChairNumber = "GHE-01" })) check(r.StatusCode == HttpStatusCode.Conflict, "Normalized chair duplicate409");
        foreach (var query in new[] { "page=0", "pageSize=101", "status=5", "keyword=" + new string('x', 101) })
        { using var r = await client.GetAsync(endpoint + "?" + query); check(r.StatusCode == HttpStatusCode.BadRequest, "Chair invalid query400"); }
        using (var r = await client.GetAsync(endpoint + "?keyword=" + Uri.EscapeDataString("phòng") + "&status=3&isActive=true"))
        { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true && json.GetProperty("totalCount").GetInt32() == 1, "Chair literal Vietnamese search/status/active/no-store"); }
        using (var r = await client.GetAsync(endpoint + "?keyword=%25")) { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(json.GetProperty("totalCount").GetInt32() == 0, "Chair percent treated literally, no wildcard"); }
        var path = endpoint + "/" + created.Id;
        using (var r = await client.PutAsJsonAsync(path, new UpdateDentalChairRequest("Phòng 3", 1, null, 1, true))) check(r.StatusCode == HttpStatusCode.OK, "Admin marks serviced maintenance chair Available200");
        using (var r = await client.PutAsJsonAsync(path, new UpdateDentalChairRequest("Stale", 4, null, 1, true))) check(r.StatusCode == HttpStatusCode.Conflict, "Stale chair version409");
        using (var r = await client.PutAsJsonAsync(path, new UpdateDentalChairRequest("Phòng 3", 4, null, 2, true)))
        { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.StatusCode == HttpStatusCode.OK && !json.GetProperty("isActive").GetBoolean() && json.GetProperty("version").GetInt32() == 3, "Chair deactivation is soft and increments version"); }
        check(await db.DentalChairs.CountAsync() == 1 && await db.AuditLogs.CountAsync(a => a.EntityType == "DentalChair" && a.EntityId == created.Id) == 3, "No deletion and exactly three catalog audit records");
        using (var r = await client.DeleteAsync(path)) check(r.StatusCode == HttpStatusCode.MethodNotAllowed, "Chair catalog has no hard-delete endpoint");
        var tracked = await db.DentalChairs.SingleAsync(c => c.Id == created.Id); tracked.Status = DentalChairStatus.Occupied; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        using (var r = await client.PutAsJsonAsync(path, new UpdateDentalChairRequest("P", 1, null, 3, true))) check(r.StatusCode == HttpStatusCode.Conflict, "Admin cannot manually free occupied chair409");
        DentalChairService Service(DentalDbContext context) => new(new DentalChairRepository(context), new UnitOfWork(context),
            new AuditLogger(context, NullLogger<AuditLogger>.Instance), new VietnamClock(TimeProvider.System));
        var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;
        await using (var context = new DentalDbContext(faultOptions))
        {
            var threw = false; try { await Service(context).CreateAsync(actorId, RoleCodes.Admin, body with { ChairNumber = "FAULT" }); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic chair audit failure") { threw = true; }
            check(threw && !await db.DentalChairs.AnyAsync(c => c.ChairNumber == "FAULT"), "Required chair create audit failure rolls back chair");
        }
        var editable = new DentalChair { ChairNumber = "EDIT", RoomName = "P", CreatedByUserId = actorId }; db.DentalChairs.Add(editable); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await using (var context = new DentalDbContext(faultOptions))
        {
            var threw = false; try { await Service(context).UpdateAsync(editable.Id, actorId, RoleCodes.Admin, new("Changed", 3, null, 1, true)); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic chair audit failure") { threw = true; }
            check(threw && await db.DentalChairs.AsNoTracking().AnyAsync(c => c.Id == editable.Id && c.RoomName == "P" && c.Version == 1 && c.Status == DentalChairStatus.Available), "Required update audit failure rolls back metadata/status/version");
        }
        var raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        async Task<Result<DentalChairResponse>> Create()
        { await using var context = new DentalDbContext(raceOptions); return await Service(context).CreateAsync(actorId, RoleCodes.Admin, body with { ChairNumber = "RACE" }); }
        var outcomes = await Task.WhenAll(Create(), Create());
        check(outcomes.Count(r => r.IsSuccess) == 1 && outcomes.Count(r => r.Error.Code == "MST_052") == 1, "Duplicate chair race commits once and returns conflict");
        check(await db.DentalChairs.CountAsync(c => c.ChairNumber == "RACE") == 1, "Concurrent create never duplicates normalized chair number");
        raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        async Task<Result<DentalChairResponse>> Update(int status)
        { await using var context = new DentalDbContext(raceOptions); return await Service(context).UpdateAsync(editable.Id, actorId, RoleCodes.Admin, new("P", status, null, 1, true)); }
        outcomes = await Task.WhenAll(Update(3), Update(4));
        check(outcomes.Count(r => r.IsSuccess) == 1 && outcomes.Count(r => r.Error.Code == "MST_052") == 1, "Concurrent chair edits commit one version and one conflict");
        check(await db.DentalChairs.AsNoTracking().AnyAsync(c => c.Id == editable.Id && c.Version == 2) && await db.AuditLogs.CountAsync(a => a.EntityType == "DentalChair" && a.EntityId == editable.Id) == 1, "Chair edit race commits single audit/version");
    }
    private sealed class Rendezvous : SaveChangesInterceptor
    {
        private int arrivals; private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        { if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); return result; }
    }
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added)) throw new InvalidOperationException("Synthetic chair audit failure");
            return ValueTask.FromResult(result);
        }
    }
}
