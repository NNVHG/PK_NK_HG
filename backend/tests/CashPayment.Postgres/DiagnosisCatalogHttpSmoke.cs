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

internal static class DiagnosisCatalogHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        Action<string> authenticate, Action<bool, string> check)
    {
        const string path = "/api/master/diagnoses";
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Owned test DB required");
        await DentalDiagnosesSeeder.SeedAsync(db); await DentalDiagnosesSeeder.SeedAsync(db); db.ChangeTracker.Clear();
        check(await db.DentalDiagnoses.CountAsync() == 30, "Seed30 internal diagnoses is idempotent");
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(path)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous catalog401");
        var input = new DiagnosisWriteRequest("test", "Viêm tủy thử nghiệm", "Synthetic description", "Bệnh lý tủy");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Receptionist, RoleCodes.Assistant })
        {
            authenticate(role); using var read = await client.GetAsync(path); using var write = await client.PostAsJsonAsync(path, input);
            check(read.StatusCode == HttpStatusCode.Forbidden && write.StatusCode == HttpStatusCode.Forbidden, "Unauthorized catalog reader/writer403");
        }
        authenticate(RoleCodes.Dentist);
        using (var r = await client.GetAsync(path + "?keyword=viem%20tuy"))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<DiagnosisResponse>>(); check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true && page!.Items.Count >= 2, "Dentist reads accentless search with NoStore"); }
        using (var r = await client.GetAsync(path + "?includeInactive=true")) check(r.StatusCode == HttpStatusCode.Forbidden, "Dentist cannot request inactive records403");
        using (var r = await client.PostAsJsonAsync(path, input)) check(r.StatusCode == HttpStatusCode.Forbidden, "Dentist cannot create403");
        authenticate(RoleCodes.Admin);
        DiagnosisResponse created;
        using (var r = await client.PostAsJsonAsync(path, input)) { check(r.StatusCode == HttpStatusCode.Created, "Admin creates diagnosis201"); created = (await r.Content.ReadFromJsonAsync<DiagnosisResponse>())!; }
        check(created.Code == "TEST" && created.Version == 1 && created.Id > 0, "Canonical unique code and initial version persisted");
        using (var r = await client.PostAsJsonAsync(path, input)) check(r.StatusCode == HttpStatusCode.Conflict, "Duplicate canonical code409");
        using (var r = await client.GetAsync(path + "?pageSize=51")) check(r.StatusCode == HttpStatusCode.BadRequest, "Bounded page size400");
        using (var r = await client.PostAsJsonAsync(path, input with { Code = "OVER", Name = new string('x', 201) })) check(r.StatusCode == HttpStatusCode.BadRequest, "Name length validated400");
        using (var r = await client.GetAsync(path + "?keyword=VIEM%20TUY%20THU%20NGHIEM"))
        { var page = await r.Content.ReadFromJsonAsync<PagedResult<DiagnosisResponse>>(); check(page!.Items.Single().Id == created.Id, "Search removes Vietnamese accents and case using persisted normalized key"); }
        var clinicalVisit = new Visit { PatientId = (await db.Patients.Select(p => p.PatientId).FirstAsync()), CreatedByUserId = actorId,
            Status = VisitStatuses.Completed, IsLocked = true, Diagnosis = created.Name };
        db.Visits.Add(clinicalVisit); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var update = input with { Code = "TEST", Name = "Tên mới", ExpectedVersion = 1, IsActive = false };
        using (var r = await client.PutAsJsonAsync(path + "/" + created.Id, update)) check(r.StatusCode == HttpStatusCode.OK, "Admin edits and deactivates diagnosis200");
        check(await db.Visits.AnyAsync(v => v.VisitId == clinicalVisit.VisitId && v.Diagnosis == created.Name), "Catalog changes never rewrite historical free-text clinical diagnoses");
        using (var r = await client.PutAsJsonAsync(path + "/" + created.Id, update)) check(r.StatusCode == HttpStatusCode.Conflict, "Stale version rejected409");
        authenticate(RoleCodes.Dentist);
        using (var r = await client.GetAsync(path + "?keyword=TEST")) check((await r.Content.ReadFromJsonAsync<PagedResult<DiagnosisResponse>>())!.TotalCount == 0, "Inactive diagnosis excluded from clinical suggestions");
        authenticate(RoleCodes.Admin);
        using (var r = await client.GetAsync(path + "?keyword=TEST&includeInactive=true")) check((await r.Content.ReadFromJsonAsync<PagedResult<DiagnosisResponse>>())!.Items.Single().Version == 2, "Admin sees inactive row with incremented version");
        using (var r = await client.PutAsJsonAsync(path + "/" + created.Id, update with { IsActive = true, ExpectedVersion = 2 })) check(r.StatusCode == HttpStatusCode.OK, "Admin reactivates existing identity200");
        await DentalDiagnosesSeeder.SeedAsync(db);
        check(await db.DentalDiagnoses.CountAsync() == 31 && await db.DentalDiagnoses.AnyAsync(d => d.Id == created.Id && d.Name == "Tên mới"), "Seeder preserves Admin edits and adds no duplicates");
        check(await db.AuditLogs.CountAsync(a => a.EntityType == "DentalDiagnosis" && a.EntityId == created.Id) == 3, "One audit per committed catalog mutation");
        var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;
        var failed = false;
        await using (var context = new DentalDbContext(faultOptions))
        {
            var service = new DiagnosisCatalogService(new DiagnosisCatalogRepository(context), new UnitOfWork(context), new AuditLogger(context, NullLogger<AuditLogger>.Instance));
            try { await service.SaveAsync(null, actorId, RoleCodes.Admin, input with { Code = "FAULT" }); }
            catch (InvalidOperationException error) when (error.Message == "Synthetic catalog audit fault") { failed = true; }
        }
        check(failed && !await db.DentalDiagnoses.AnyAsync(d => d.Code == "FAULT"), "Required audit fault rolls back saved catalog row");
        async Task<bool> Race()
        {
            await using var context = new DentalDbContext(options);
            var service = new DiagnosisCatalogService(new DiagnosisCatalogRepository(context), new UnitOfWork(context), new AuditLogger(context, NullLogger<AuditLogger>.Instance));
            return (await service.SaveAsync(null, actorId, RoleCodes.Admin, input with { Code = "RACE" })).IsSuccess;
        }
        check((await Task.WhenAll(Race(), Race())).Count(success => success) == 1 && await db.DentalDiagnoses.CountAsync(d => d.Code == "RACE") == 1, "Concurrent duplicate creation persists one code with controlled conflict");
    }
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.EntityType == "DentalDiagnosis")) throw new InvalidOperationException("Synthetic catalog audit fault");
            return ValueTask.FromResult(result);
        }
    }
}
