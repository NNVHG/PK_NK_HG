using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.ServiceCatalog.DTOs;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class FdiServiceSearchHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, int patientId,
        Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Service search fixtures require the owned isolated database.");
        var prefix = "SEARCH-" + Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;
        DentalService Service(string suffix, bool active, decimal amount, bool future = false)
            => new() { Code = prefix + suffix, Name = "Trám Răng " + prefix + suffix, IsActive = active, DurationMinutes = 15,
                Prices = [new ServicePrice { Amount = amount, EffectiveFrom = future ? now.AddDays(1) : now.AddDays(-1), CreatedByUserId = actorId }] };
        var priced = Service("-A", true, 100000); var unpriced = Service("-B", true, 900000, true);
        var inactive = Service("-C", false, 50000); var percent = Service("-100%", true, 120000);
        db.DentalServices.AddRange(priced, unpriced, inactive, percent); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var auditCount = await db.AuditLogs.CountAsync();
        string Search(string keyword, string rest = "") => "/api/services?isActive=true&keyword=" + Uri.EscapeDataString(keyword) + rest;
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(Search(prefix))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Service search anonymous401");
        foreach (var denied in new[] { RoleCodes.Patient, RoleCodes.Receptionist })
        { authenticate(denied); using var r = await client.GetAsync(Search(prefix)); check(r.StatusCode == HttpStatusCode.Forbidden, $"Service search {denied}403"); }
        foreach (var allowed in new[] { RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(allowed); using var r = await client.GetAsync(Search(prefix.ToLowerInvariant()));
            var page = await r.Content.ReadFromJsonAsync<PagedResult<ServiceCatalogListItemResponse>>();
            check(r.StatusCode == HttpStatusCode.OK && page!.TotalCount == 3 && page.Items.All(x => x.IsActive), $"Service search {allowed} active/code filter200");
            check(r.Headers.CacheControl?.NoStore == true, "Service search no-store");
            check(page!.Items.Single(x => x.DentalServiceId == priced.DentalServiceId).CurrentPrice == 100000 &&
                page.Items.Single(x => x.DentalServiceId == unpriced.DentalServiceId).CurrentPrice == null, "Service search effective price and future-only price honest");
        }
        authenticate(RoleCodes.Admin);
        foreach (var query in new[] { "page=0", "page=1000001", "page=2147483647", "pageSize=101", "keyword=" + new string('x', 101) })
        { using var r = await client.GetAsync("/api/services?" + query); check(r.StatusCode == HttpStatusCode.BadRequest, "Service search invalid bound400"); }
        using (var r = await client.GetAsync(Search("TRÁM RĂNG " + prefix)))
            check((await r.Content.ReadFromJsonAsync<PagedResult<ServiceCatalogListItemResponse>>())!.TotalCount == 3, "Service search Vietnamese name ignores case");
        using (var r = await client.GetAsync(Search(prefix + "-100%")))
        {
            var page = (await r.Content.ReadFromJsonAsync<PagedResult<ServiceCatalogListItemResponse>>())!;
            check(page.TotalCount == 1 && page.Items.Single().DentalServiceId == percent.DentalServiceId, "Service search percent is literal not wildcard");
        }
        using (var r = await client.GetAsync(Search(prefix + "-MISSING")))
            check((await r.Content.ReadFromJsonAsync<PagedResult<ServiceCatalogListItemResponse>>())!.TotalCount == 0, "Service search missing is honest empty");
        var found = new List<int>();
        for (var pageNumber = 1; pageNumber <= 3; pageNumber++)
        {
            using var r = await client.GetAsync(Search(prefix, "&pageSize=1&page=" + pageNumber));
            var page = (await r.Content.ReadFromJsonAsync<PagedResult<ServiceCatalogListItemResponse>>())!;
            check(page.TotalCount == 3 && page.TotalPages == 3 && page.Items.Count == 1, "Service search filtered pagination stable count");
            found.Add(page.Items.Single().DentalServiceId);
        }
        check(found.Distinct().Count() == 3 && !found.Contains(inactive.DentalServiceId), "Service search pages neither repeat nor include inactive service");
        check(await db.AuditLogs.CountAsync() == auditCount, "Service search GET never writes audit");
        var visit = new Visit { PatientId = patientId, CreatedByUserId = actorId, DentistId = actorId, Status = VisitStatuses.InProgress };
        db.Visits.Add(visit); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.ServicePrices.Add(new ServicePrice { ServiceId = priced.DentalServiceId, Amount = 210000, EffectiveFrom = DateTime.UtcNow, CreatedByUserId = actorId });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var assignPath = $"/api/visits/{visit.VisitId}/services";
        using (var r = await client.PostAsJsonAsync(assignPath, new AssignServicesRequest(priced.DentalServiceId, [11, 51], "B", 2)))
        {
            var rows = (await r.Content.ReadFromJsonAsync<AssignedServiceResponse[]>())!;
            check(r.StatusCode == HttpStatusCode.Created && rows!.Length == 2 && rows.All(x => x.UnitPrice == 210000 && x.TotalAmount == 420000),
                "Search-to-assignment rechecks effective price at write instead of stale lookup price");
            check(rows!.Select(x => x.ToothNumber).Order().SequenceEqual(new int?[] { 11, 51 }) && rows.All(x => x.Surface == "B"),
                "Search-to-assignment persists adult and child FDI tooth/surface");
        }
        var stored = await db.VisitServices.AsNoTracking().Where(x => x.VisitId == visit.VisitId).ToListAsync();
        check(stored.Count == 2 && stored.All(x => x.UnitPrice == 210000 && x.CreatedByUserId == actorId), "Search-to-assignment persists actual price snapshot and actor");
        using (var r = await client.PostAsJsonAsync(assignPath, new AssignServicesRequest(unpriced.DentalServiceId, [11], null)))
            check(r.StatusCode == HttpStatusCode.Conflict, "Search-to-assignment rejects future-only price409");
        var deactivate = await db.DentalServices.SingleAsync(x => x.DentalServiceId == priced.DentalServiceId);
        deactivate.IsActive = false; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        using (var r = await client.PostAsJsonAsync(assignPath, new AssignServicesRequest(priced.DentalServiceId, [11], null)))
            check(r.StatusCode == HttpStatusCode.Conflict, "Search-to-assignment rechecks inactive service409");
        check(await db.VisitServices.CountAsync(x => x.VisitId == visit.VisitId) == 2, "Search rejected assignments never add rows");
    }
}
