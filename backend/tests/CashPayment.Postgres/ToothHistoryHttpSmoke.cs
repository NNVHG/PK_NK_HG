using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
internal static class ToothHistoryHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, int otherPatientId,
        Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var now = DateTime.UtcNow;
        var patient = new Patient { FullName = "Synthetic history profile", DateOfBirth = new(1990, 1, 1), Phone = "0999999993", UserId = actorId };
        var oldVisit = new Visit { Patient = patient, CreatedByUserId = actorId, DentistId = actorId, Status = VisitStatuses.Completed, IsLocked = true, StartedAt = now.AddDays(-3) };
        var newVisit = new Visit { Patient = patient, CreatedByUserId = actorId, DentistId = actorId, Status = VisitStatuses.InProgress, StartedAt = now.AddHours(-3) };
        db.ToothConditions.AddRange(new ToothCondition { Visit = oldVisit, ToothNumber = 11, ConditionCode = "CARIES", Surface = "O", Note = "Synthetic old note", CreatedAt = now.AddDays(-3) },
            new ToothCondition { Visit = newVisit, ToothNumber = 11, ConditionCode = "RESTORED", Surface = "O", CreatedAt = now.AddHours(-2) },
            new ToothCondition { Visit = newVisit, ToothNumber = 51, ConditionCode = "NORMAL", CreatedAt = now.AddHours(-1) });
        var catalog = new DentalService { Code = "TEST-HISTORY", Name = "Synthetic history service", DurationMinutes = 10 };
        var price = new ServicePrice { Service = catalog, Amount = 10000, EffectiveFrom = now.AddDays(-4), CreatedByUserId = actorId };
        db.ServicePrices.Add(price); await db.SaveChangesAsync();
        var serviceOnlyVisit = new Visit { PatientId = patient.PatientId, CreatedByUserId = actorId, Status = VisitStatuses.Completed, IsLocked = true };
        VisitService Assignment(Visit visit, int? tooth, DateTime date) => new() { Visit = visit, ToothNumber = tooth, ServiceId = catalog.DentalServiceId,
            ServicePriceId = price.ServicePriceId, ServiceCode = catalog.Code, ServiceName = catalog.Name, UnitPrice = 10000, Quantity = 2,
            CreatedByUserId = actorId, CreatedAt = date };
        db.VisitServices.AddRange(Assignment(newVisit, 11, now.AddMinutes(-30)), Assignment(newVisit, null, now.AddMinutes(-10)),
            Assignment(serviceOnlyVisit, 18, now.AddMinutes(-20)));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var auditsBefore = await db.AuditLogs.CountAsync();
        var root = $"/api/patients/{patient.PatientId}";
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync(root + "/teeth/11/history")) check(response.StatusCode == HttpStatusCode.Unauthorized, "History anonymous 401");
        authenticate("UNKNOWN");
        using (var response = await client.GetAsync(root + "/treatment-timeline")) check(response.StatusCode == HttpStatusCode.Forbidden, "History unknown role 403");
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Receptionist, RoleCodes.Assistant })
        { authenticate(role); using var response = await client.GetAsync(root + "/teeth/11/history"); check(response.StatusCode == HttpStatusCode.OK, $"History {role} 200"); }
        authenticate(RoleCodes.Patient);
        using (var response = await client.GetAsync(root + "/teeth/11/history"))
        {
            var page = await response.Content.ReadFromJsonAsync<PagedResult<ToothHistoryItemResponse>>();
            check(response.StatusCode == HttpStatusCode.OK && page?.TotalCount == 3 && page.Items.All(e => e.ToothNumber == 11), "History patient ownership and tooth filter");
            check(response.Headers.CacheControl?.NoStore == true, "History no-store");
            check(page!.Items.Select(e => e.CreatedAt).SequenceEqual(page.Items.Select(e => e.CreatedAt).OrderDescending()), "History newest first across visits");
            check(page.Items.Count(e => e.ConditionCode is not null) == 2 && page.Items.Any(e => e.Note == "Synthetic old note"), "History preserves old condition and note");
            check(page.Items.All(e => !string.IsNullOrWhiteSpace(e.DentistName)), "History projects actual assigned dentist names");
            var service = page.Items.Single(e => e.EventType == "ServiceAssignment");
            check(service.Services.Count == 1 && service.Services[0].UnitPrice == 10000 && service.Services[0].Quantity == 2 && service.ConditionCode is null,
                "History service snapshot appears exactly once and is not fabricated condition");
        }
        using (var response = await client.GetAsync($"/api/patients/{otherPatientId}/treatment-timeline")) check(response.StatusCode == HttpStatusCode.NotFound, "History unrelated patient 404");
        using (var response = await client.GetAsync("/api/patients/2147483647/treatment-timeline")) check(response.StatusCode == HttpStatusCode.NotFound, "History missing patient 404");
        using (var response = await client.GetAsync("/api/patients/0/treatment-timeline")) check(response.StatusCode == HttpStatusCode.BadRequest, "History invalid patient ID 400");
        foreach (var path in new[] { "/teeth/99/history", "/teeth/11/history?page=0", "/treatment-timeline?toothNumber=56", "/treatment-timeline?pageSize=51" })
        { using var response = await client.GetAsync(root + path); check(response.StatusCode == HttpStatusCode.BadRequest, "History invalid FDI/page 400"); }
        using (var response = await client.GetAsync(root + "/treatment-timeline"))
        {
            var page = await response.Content.ReadFromJsonAsync<PagedResult<ToothHistoryItemResponse>>();
            check(page?.TotalCount == 6 && page.Items.Any(e => e.ToothNumber == 51) && page.Items.Any(e => e.ToothNumber is null), "History all-jaw contains child and general service events");
            check(page!.Items.Any(e => e.VisitId == serviceOnlyVisit.VisitId && e.Services.Count == 1), "History includes visits with services but no condition");
        }
        using (var response = await client.GetAsync(root + "/treatment-timeline?toothNumber=11"))
            check((await response.Content.ReadFromJsonAsync<PagedResult<ToothHistoryItemResponse>>())?.TotalCount == 3, "History timeline optional tooth filter");
        var first = await client.GetFromJsonAsync<PagedResult<ToothHistoryItemResponse>>(root + "/teeth/11/history?pageSize=1");
        var second = await client.GetFromJsonAsync<PagedResult<ToothHistoryItemResponse>>(root + "/teeth/11/history?pageSize=1&page=2");
        check(first?.TotalPages == 3 && first.Items.Count == 1 && second?.Items.Count == 1 && first.Items[0].EventType != second.Items[0].EventType, "History pagination retains all events without repetition");
        using (var response = await client.GetAsync(root + "/teeth/85/history")) check((await response.Content.ReadFromJsonAsync<PagedResult<ToothHistoryItemResponse>>())?.TotalCount == 0, "History valid empty tooth is 200 empty");
        check(await db.AuditLogs.CountAsync() == auditsBefore && await db.ToothConditions.CountAsync(c => c.Visit.PatientId == patient.PatientId) == 3,
            "History GET is read-only and never overwrites clinical records");
    }
}
