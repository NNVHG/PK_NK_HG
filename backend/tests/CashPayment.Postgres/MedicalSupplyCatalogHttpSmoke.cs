using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

internal static class MedicalSupplyCatalogHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate, Action<bool, string> check)
    {
        const string path = "/api/master/medical-supplies";
        var input = new MedicalSupplyWriteRequest("vt-gangtay", "Găng tay y tế", "Hộp", 10, "Hộp 100 chiếc", IsActive: true);
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(path)) check(r.StatusCode == HttpStatusCode.Unauthorized, "MedicalSupply anonymous401");
        foreach (var role in new[] { RoleCodes.Receptionist, RoleCodes.Assistant, RoleCodes.Patient })
        {
            authenticate(role);
            using var get = await client.GetAsync(path);
            using var post = await client.PostAsJsonAsync(path, input);
            using var put = await client.PutAsJsonAsync(path + "/1", input);
            using var delete = await client.DeleteAsync(path + "/1?expectedVersion=1");
            check(get.StatusCode == HttpStatusCode.Forbidden && post.StatusCode == HttpStatusCode.Forbidden && put.StatusCode == HttpStatusCode.Forbidden && delete.StatusCode == HttpStatusCode.Forbidden, role + " supply catalog read/write403");
        }
        authenticate(RoleCodes.Dentist);
        using (var r = await client.GetAsync(path)) check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true, "Dentist reads active supply catalog with NoStore");
        using (var r = await client.GetAsync(path + "?includeInactive=true")) check(r.StatusCode == HttpStatusCode.Forbidden, "Dentist cannot request inactive supplies403");
        using (var r = await client.PostAsJsonAsync(path, input)) check(r.StatusCode == HttpStatusCode.Forbidden, "Dentist cannot create supply403");
        authenticate(RoleCodes.Admin);
        MedicalSupplyResponse created;
        using (var r = await client.PostAsJsonAsync(path, input))
        {
            check(r.StatusCode == HttpStatusCode.Created, "Admin creates medical supply201");
            created = (await r.Content.ReadFromJsonAsync<MedicalSupplyResponse>())!;
            check(created.Code == "VT-GANGTAY" && created.IsActive && created.MinimumStockThreshold == 10 && created.Version == 1, "Creation canonicalizes code/threshold/state/version");
        }
        using (var r = await client.PostAsJsonAsync(path, input with { Code = "Vt-GangTay" })) check(r.StatusCode == HttpStatusCode.Conflict, "Canonical duplicate supply code409");
        foreach (var query in new[] { "?page=0", "?pageSize=51", "?page=1000001", "?keyword=" + new string('x', 201) })
        {
            using var r = await client.GetAsync(path + query);
            check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid supply query400");
        }
        foreach (var bad in new[]
        {
            input with { Code = "bad code" },
            input with { Name = new string('x', 201) },
            input with { Unit = " " },
            input with { Unit = new string('u', 51) },
            input with { MinimumStockThreshold = -1 },
            input with { MinimumStockThreshold = 1000001 },
            input with { Description = new string('d', 501) }
        })
        {
            using var r = await client.PostAsJsonAsync(path, bad with { Code = bad.Code == "bad code" ? bad.Code : "BAD" });
            check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid supply metadata/threshold400");
        }
        using (var r = await client.GetAsync(path + "?keyword=GANG%20TAY"))
        {
            check((await r.Content.ReadFromJsonAsync<PagedResult<MedicalSupplyResponse>>())!.Items.Single().Id == created.Id, "Search matches supply name case/accent-insensitively");
        }
        await using var db = new DentalDbContext(options);
        var persistedCreation = await db.MedicalSupplies.Where(m => m.Id == created.Id).Select(m => m.CreatedAt).SingleAsync();
        var update = input with { MinimumStockThreshold = 25, ExpectedVersion = 1 };
        using (var r = await client.PutAsJsonAsync(path + "/" + created.Id, update))
        {
            check(r.StatusCode == HttpStatusCode.OK, "Admin updates supply threshold200");
        }
        using (var r = await client.PutAsJsonAsync(path + "/" + created.Id, update with { MinimumStockThreshold = 5 })) check(r.StatusCode == HttpStatusCode.Conflict, "Stale supply version409 no overwrite");
        using (var r = await client.DeleteAsync(path + "/" + created.Id + "?expectedVersion=1")) check(r.StatusCode == HttpStatusCode.Conflict, "Stale supply deactivation409");
        using (var r = await client.DeleteAsync(path + "/" + created.Id + "?expectedVersion=2")) check(r.StatusCode == HttpStatusCode.OK, "DELETE performs soft deactivation200");
        db.ChangeTracker.Clear();
        var stored = await db.MedicalSupplies.SingleAsync(m => m.Id == created.Id);
        check(!stored.IsActive && stored.MinimumStockThreshold == 25 && stored.Version == 3 && stored.UpdatedAt.HasValue && stored.CreatedAt == persistedCreation, "Soft deactivation preserves threshold/identity/creation and increments version");
        using (var r = await client.DeleteAsync(path + "/" + created.Id + "?expectedVersion=3")) check(r.StatusCode == HttpStatusCode.OK, "Repeat soft deactivation idempotent at current version");
        check(await db.AuditLogs.CountAsync(a => a.EntityType == "MedicalSupply" && a.EntityId == created.Id) == 3, "Exactly one metadata audit per committed supply mutation");
        var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;
        async Task<bool> Fault(bool deactivate)
        {
            await using var context = new DentalDbContext(faultOptions);
            var service = new MedicalSupplyCatalogService(new MedicalSupplyCatalogRepository(context), new UnitOfWork(context), new AuditLogger(context, NullLogger<AuditLogger>.Instance), new VietnamClock(TimeProvider.System));
            try
            {
                if (deactivate) await service.DeactivateAsync(created.Id, 3, actorId, RoleCodes.Admin);
                else await service.SaveAsync(null, actorId, RoleCodes.Admin, input with { Code = "FAULT" });
            }
            catch (InvalidOperationException e) when (e.Message == "Synthetic supply audit fault")
            {
                return true;
            }
            return false;
        }
        check(await Fault(false) && !await db.MedicalSupplies.AnyAsync(m => m.Code == "FAULT"), "Required audit failure rolls back created supply");
        using (var r = await client.DeleteAsync(path + "/999999?expectedVersion=1")) check(r.StatusCode == HttpStatusCode.NotFound, "Missing supply deactivation404");
        using (var r = await client.DeleteAsync(path + "/" + created.Id)) check(r.StatusCode == HttpStatusCode.BadRequest, "Missing expectedVersion400");
        check(db.Model.FindEntityType(typeof(MedicalSupply))!.FindProperty("UnitPrice") is null && db.Model.FindEntityType(typeof(MedicalSupply))!.FindProperty("StockQuantity") is null, "MedicalSupply master catalog has no UnitPrice or StockQuantity per DL-103");
    }

    private sealed class FailAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.EntityType == "MedicalSupply"))
                throw new InvalidOperationException("Synthetic supply audit fault");
            return ValueTask.FromResult(result);
        }
    }
}
