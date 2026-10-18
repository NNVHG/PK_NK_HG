using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Data.Seeders;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
internal static class MedicineCatalogHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate, Action<bool, string> check)
    {
        const string path = "/api/medicines";
        var input = new MedicineWriteRequest("test", "Thuốc kiểm thử", "Hoạt chất kiểm thử", "Hàm lượng kiểm thử", "Viên", 0, IsActive: false);
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(path)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Medicine anonymous401");
        foreach (var role in new[] { RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Patient })
        {
            authenticate(role);
            using var get = await client.GetAsync(path); using var post = await client.PostAsJsonAsync(path, input);
            using var put = await client.PutAsJsonAsync(path + "/1", input); using var delete = await client.DeleteAsync(path + "/1?expectedVersion=1");
            check(role == RoleCodes.Dentist
                ? get.StatusCode == HttpStatusCode.OK && get.Headers.CacheControl?.NoStore == true
                : get.StatusCode == HttpStatusCode.Forbidden, role + " master catalog read follows role policy");
            check(post.StatusCode == HttpStatusCode.Forbidden && put.StatusCode == HttpStatusCode.Forbidden && delete.StatusCode == HttpStatusCode.Forbidden, role + " master catalog write403");
        }
        await using var db = new DentalDbContext(options);
        await MedicinesSeeder.SeedAsync(db);
        check(await db.Medicines.CountAsync() == 30 && await db.Medicines.AllAsync(m => !m.IsActive && m.UnitPrice == 0 && m.DefaultUsageInstruction == null && m.Strength == null), "Seed30 inactive/zero with no invented strength/dosage Q312a");
        await MedicinesSeeder.SeedAsync(db); check(await db.Medicines.CountAsync() == 30, "Seed repeat does not duplicate names");
        authenticate(RoleCodes.Admin);
        using (var r = await client.GetAsync(path)) check((await r.Content.ReadFromJsonAsync<PagedResult<MedicineResponse>>())!.TotalCount == 0 && r.Headers.CacheControl?.NoStore == true, "Default active list empty and privateNoStore");
        using (var r = await client.GetAsync(path + "?includeInactive=true")) { var page = (await r.Content.ReadFromJsonAsync<PagedResult<MedicineResponse>>())!; check(page.Items.Count == 20 && page.TotalCount == 30 && page.HasNextPage, "Admin sees inactive seed in real pagination"); }
        using (var r = await client.GetAsync(path + "?includeInactive=true&page=2")) check((await r.Content.ReadFromJsonAsync<PagedResult<MedicineResponse>>())!.Items.Count == 10, "Second page has ten seeded medicines");
        using (var r = await client.PostAsJsonAsync(path, input with { IsActive = true })) check(r.StatusCode == HttpStatusCode.BadRequest, "Cannot activate zero-priced medicine400");
        MedicineResponse created;
        using (var r = await client.PostAsJsonAsync(path, input))
        { check(r.StatusCode == HttpStatusCode.Created, "Admin creates inactive medicine201"); created = (await r.Content.ReadFromJsonAsync<MedicineResponse>())!; check(created.Code == "TEST" && !created.IsActive && created.UnitPrice == 0 && created.Version == 1, "Creation canonicalizes code/price/state/version"); }
        using (var r = await client.PostAsJsonAsync(path, input with { Code = "TeSt" })) check(r.StatusCode == HttpStatusCode.Conflict, "Canonical duplicate code409");
        foreach (var query in new[] { "?page=0", "?pageSize=51", "?page=1000001", "?keyword=" + new string('x', 201) })
        { using var r = await client.GetAsync(path + query); check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid medicine query400"); }
        foreach (var bad in new[] { input with { Unit = "Khác" }, input with { UnitPrice = .5m }, input with { UnitPrice = -1 }, input with { UnitPrice = 1000000000000000000m }, input with { Name = new string('x', 201) }, input with { Strength = new string('x', 51) }, input with { DefaultUsageInstruction = new string('x', 501) } })
        { using var r = await client.PostAsJsonAsync(path, bad with { Code = "BAD" }); check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid medicine metadata/price400"); }
        using (var r = await client.GetAsync(path + "?includeInactive=true&keyword=HOAT%20CHAT")) check((await r.Content.ReadFromJsonAsync<PagedResult<MedicineResponse>>())!.Items.Single().Id == created.Id, "Search matches ingredient case/accent-insensitively");
        var persistedCreation = await db.Medicines.Where(m => m.Id == created.Id).Select(m => m.CreatedAt).SingleAsync();
        var update = input with { UnitPrice = 999999999999999999m, IsActive = true, ExpectedVersion = 1 };
        using (var r = await client.PutAsJsonAsync(path + "/" + created.Id, update))
        { var json = await r.Content.ReadAsStringAsync(); check(r.StatusCode == HttpStatusCode.OK && json.Contains("\"unitPrice\":\"999999999999999999\""), "Exact18digit JSON price string, active update200"); }
        using (var r = await client.PutAsJsonAsync(path + "/" + created.Id, update with { UnitPrice = 500 })) check(r.StatusCode == HttpStatusCode.Conflict, "Stale price version409 no overwrite");
        using (var r = await client.GetAsync(path)) check((await r.Content.ReadFromJsonAsync<PagedResult<MedicineResponse>>())!.Items.Single().UnitPrice == 999999999999999999m, "Only activated medicine offered in active catalog");
        using (var r = await client.DeleteAsync(path + "/" + created.Id + "?expectedVersion=1")) check(r.StatusCode == HttpStatusCode.Conflict, "Stale deactivation409");
        using (var r = await client.DeleteAsync(path + "/" + created.Id + "?expectedVersion=2")) check(r.StatusCode == HttpStatusCode.OK, "DELETE performs soft deactivation200");
        db.ChangeTracker.Clear(); var stored = await db.Medicines.SingleAsync(m => m.Id == created.Id);
        check(!stored.IsActive && stored.UnitPrice == 999999999999999999m && stored.Version == 3 && stored.UpdatedAt.HasValue && stored.CreatedAt == persistedCreation, "Soft deactivation preserves price/identity/creation and increments version");
        using (var r = await client.DeleteAsync(path + "/" + created.Id + "?expectedVersion=3")) check(r.StatusCode == HttpStatusCode.OK, "Repeat soft deactivation idempotent at current version");
        check(await db.AuditLogs.CountAsync(a => a.EntityType == "Medicine" && a.EntityId == created.Id) == 3, "Exactly one metadata audit per committed mutation, no replay audit");
        await MedicinesSeeder.SeedAsync(db); check(await db.Medicines.CountAsync() == 31 && await db.Medicines.AnyAsync(m => m.Id == created.Id && m.UnitPrice == 999999999999999999m), "Seeder never overwrites Admin price/activation changes");
        using (var r = await client.PutAsJsonAsync(path + "/" + created.Id, input with { UnitPrice = 500, IsActive = true, ExpectedVersion = 3 })) check(r.StatusCode == HttpStatusCode.OK, "Admin can reactivate after setting intended price");
        var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;
        async Task<bool> Fault(bool deactivate)
        {
            await using var context = new DentalDbContext(faultOptions);
            var service = new MedicineCatalogService(new MedicineCatalogRepository(context), new UnitOfWork(context), new AuditLogger(context, NullLogger<AuditLogger>.Instance), new VietnamClock(TimeProvider.System));
            try { if (deactivate) await service.DeactivateAsync(created.Id, 4, actorId, RoleCodes.Admin); else await service.SaveAsync(null, actorId, RoleCodes.Admin, input with { Code = "FAULT" }); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic medicine audit fault") { return true; }
            return false;
        }
        check(await Fault(false) && !await db.Medicines.AnyAsync(m => m.Code == "FAULT"), "Required audit failure rolls back created medicine");
        check(await Fault(true) && await db.Medicines.AnyAsync(m => m.Id == created.Id && m.IsActive && m.Version == 4), "Required audit failure rolls back soft deactivation and version");
        async Task<bool> Race(bool create)
        {
            await using var context = new DentalDbContext(options);
            var service = new MedicineCatalogService(new MedicineCatalogRepository(context), new UnitOfWork(context), new AuditLogger(context, NullLogger<AuditLogger>.Instance), new VietnamClock(TimeProvider.System));
            return (await service.SaveAsync(create ? null : created.Id, actorId, RoleCodes.Admin, input with { Code = create ? "RACE" : "TEST", UnitPrice = 1000, IsActive = true, ExpectedVersion = create ? 0 : 4 })).IsSuccess;
        }
        check((await Task.WhenAll(Race(true), Race(true))).Count(ok => ok) == 1 && await db.Medicines.CountAsync(m => m.Code == "RACE") == 1, "Duplicate code race yields one medicine/one conflict");
        check((await Task.WhenAll(Race(false), Race(false))).Count(ok => ok) == 1, "Concurrent same-version price edits cannot overwrite each other");
        using (var r = await client.DeleteAsync(path + "/999999?expectedVersion=1")) check(r.StatusCode == HttpStatusCode.NotFound, "Missing deactivation404");
        using (var r = await client.DeleteAsync(path + "/" + created.Id)) check(r.StatusCode == HttpStatusCode.BadRequest, "Missing expectedVersion400");
        check(db.Model.FindEntityType(typeof(Medicine))!.FindProperty("StockQuantity") is null && await db.PaymentTransactions.CountAsync() == 0, "Master catalog adds no inventory balance or financial transactions");
    }
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.EntityType == "Medicine")) throw new InvalidOperationException("Synthetic medicine audit fault");
            return ValueTask.FromResult(result);
        }
    }
}
