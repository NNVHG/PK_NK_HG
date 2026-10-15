using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.ServiceCatalog.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class ServiceCatalogHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options,
        Action<string> authenticate, Action<bool, string> check)
    {
        const string root = "/api/services";
        var initialTime = DateTimeOffset.UtcNow;
        var input = new CreateDentalServiceRequest("syn-mst", "Synthetic catalog service", "Synthetic description", 30, 100001, initialTime);
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync(root)) check(response.StatusCode == HttpStatusCode.Unauthorized, "Catalog anonymous read unauthorized");
        foreach (var role in new[] { RoleCodes.Receptionist, RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role);
            using var response = await client.PostAsJsonAsync(root, input);
            check(response.StatusCode == HttpStatusCode.Forbidden, $"Catalog {role} cannot create service");
        }
        authenticate(RoleCodes.Admin);
        foreach (var invalid in new[] { input with { InitialPrice = .5m }, input with { InitialPrice = -1 },
            input with { EffectiveFrom = initialTime.AddDays(-1) }, input with { Code = new string('X', 51) }, input with { DurationMinutes = 0 } })
        {
            using var response = await client.PostAsJsonAsync(root, invalid);
            check(response.StatusCode == HttpStatusCode.BadRequest, "Catalog validates whole VND/date/code/duration");
        }
        ServiceCatalogDetailResponse created;
        using (var response = await client.PostAsJsonAsync(root, input))
        {
            check(response.StatusCode == HttpStatusCode.Created, "Admin creates service with initial price");
            created = (await response.Content.ReadFromJsonAsync<ServiceCatalogDetailResponse>())!;
            check(created.Code == "SYN-MST" && created.IsActive && created.CurrentPrice == 100001 && created.PriceHistory?.Count == 1,
                "Catalog canonical code/current price/history persisted");
        }
        var path = root + "/" + created.DentalServiceId;
        using (var response = await client.PostAsJsonAsync(root, input)) check(response.StatusCode == HttpStatusCode.Conflict, "Duplicate catalog code conflict");
        using (var response = await client.GetAsync(root + "?keyword=SYN-MST&isActive=true&page=1&pageSize=20"))
        {
            var list = await response.Content.ReadFromJsonAsync<PagedResult<ServiceCatalogListItemResponse>>();
            check(response.IsSuccessStatusCode && list?.Items.Single().DentalServiceId == created.DentalServiceId, "Search and active filter return real service");
            check(response.Headers.CacheControl?.NoStore == true, "Catalog list no-store");
        }
        using (var response = await client.PutAsJsonAsync(path, new UpdateDentalServiceRequest("Synthetic updated", "Synthetic edited", 45)))
        {
            var updated = await response.Content.ReadFromJsonAsync<ServiceCatalogDetailResponse>();
            check(response.IsSuccessStatusCode && updated?.Code == created.Code && updated.Name == "Synthetic updated" && updated.DurationMinutes == 45,
                "Update metadata keeps code immutable");
        }
        var futureTime = initialTime.AddDays(1);
        using (var response = await client.PostAsJsonAsync(path + "/prices", new CreateServicePriceRequest(200001, futureTime)))
            check(response.StatusCode == HttpStatusCode.Created, "Append future price succeeds");
        using (var response = await client.GetAsync(path))
        {
            var detail = await response.Content.ReadFromJsonAsync<ServiceCatalogDetailResponse>();
            check(response.IsSuccessStatusCode && detail?.CurrentPrice == 100001 && detail.PriceHistory?.Count == 2 && detail.PriceHistory[0].Amount == 200001,
                "Future price appears in history without changing today's effective price");
        }
        using (var response = await client.PostAsJsonAsync(path + "/prices", new CreateServicePriceRequest(300001, initialTime)))
            check(response.StatusCode == HttpStatusCode.Conflict, "Cannot insert price before latest scheduled effective time");
        using (var response = await client.PostAsJsonAsync(path + "/prices", new CreateServicePriceRequest(300001, initialTime.AddDays(-1))))
            check(response.StatusCode == HttpStatusCode.BadRequest, "Cannot backdate price before today");
        using (var response = await client.PostAsJsonAsync(path + "/prices", new CreateServicePriceRequest(.5m, futureTime.AddDays(1))))
            check(response.StatusCode == HttpStatusCode.BadRequest, "Cannot silently round fractional service price in database");
        using (var response = await client.PostAsync(path + "/deactivate", null)) check(response.IsSuccessStatusCode, "Admin deactivates service");
        using (var response = await client.GetAsync(root + "?keyword=SYN-MST&isActive=true"))
            check((await response.Content.ReadFromJsonAsync<PagedResult<ServiceCatalogListItemResponse>>())?.TotalCount == 0, "Inactive service omitted from active picker");
        using (var response = await client.GetAsync(root + "?keyword=SYN-MST&isActive=false"))
            check((await response.Content.ReadFromJsonAsync<PagedResult<ServiceCatalogListItemResponse>>())?.TotalCount == 1, "Inactive service remains available to Admin filter");
        foreach (var role in new[] { RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role);
            using var read = await client.GetAsync(path);
            var detail = await read.Content.ReadFromJsonAsync<ServiceCatalogDetailResponse>();
            using var write = await client.PostAsJsonAsync(path + "/prices", new CreateServicePriceRequest(9, futureTime.AddDays(1)));
            check(read.IsSuccessStatusCode && detail?.PriceHistory is null && write.StatusCode == HttpStatusCode.Forbidden,
                $"Catalog {role} reads metadata but cannot read price history or write price");
        }
        authenticate(RoleCodes.Admin);
        using (var response = await client.PostAsync(path + "/activate", null)) check(response.IsSuccessStatusCode, "Admin reactivates service");
        await using var db = new DentalDbContext(options);
        check(await db.ServicePrices.CountAsync(x => x.ServiceId == created.DentalServiceId) == 2, "Only valid appended prices exist in PostgreSQL");
        check(await db.AuditLogs.CountAsync(x => x.EntityType == nameof(DentalService) && x.EntityId == created.DentalServiceId) == 5,
            "Create/update/price/deactivate/activate have actor audit entries");
        var oldPrice = await db.ServicePrices.SingleAsync(x => x.ServicePriceId == created.PriceHistory![0].ServicePriceId);
        oldPrice.Amount = 123;
        try { await db.SaveChangesAsync(); check(false, "Old service price must reject update"); }
        catch (InvalidOperationException) { check(true, "Old service price cannot be overwritten"); }
    }
}
