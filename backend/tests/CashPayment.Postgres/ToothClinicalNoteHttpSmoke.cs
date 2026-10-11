using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
internal static class ToothClinicalNoteHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, int otherPatientId,
        Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        // Reuse the owned synthetic profile created by the history helper; current schema has unique UserId.
        var patient = await db.Patients.SingleAsync(p => p.UserId == actorId);
        var visit = new Visit { Patient = patient, DentistId = actorId, CreatedByUserId = actorId, Status = VisitStatuses.InProgress };
        db.Visits.Add(visit); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var path = $"/api/visits/{visit.VisitId}/tooth-conditions";
        var body = new ToothConditionRequest(11, "O", "CARIES", "  Synthetic clinical note\nSecond line  ");
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.PostAsJsonAsync(path, body)) check(response.StatusCode == HttpStatusCode.Unauthorized, "Tooth note anonymous 401");
        foreach (var role in new[] { RoleCodes.Receptionist, RoleCodes.Assistant, RoleCodes.Patient })
        { authenticate(role); using var response = await client.PostAsJsonAsync(path, body); check(response.StatusCode == HttpStatusCode.Forbidden, $"Tooth note {role} cannot write"); }
        authenticate(RoleCodes.Dentist);
        using (var response = await client.PostAsJsonAsync(path, body with { Note = new string('x', 501) })) check(response.StatusCode == HttpStatusCode.BadRequest, "Tooth note 501 chars 400");
        using (var response = await client.PostAsJsonAsync(path, body))
        {
            var saved = await response.Content.ReadFromJsonAsync<ToothConditionResponse>();
            check(response.StatusCode == HttpStatusCode.Created && saved?.Note == body.Note!.Trim() && saved.ToothNumber == 11 && saved.Surface == "O", "Tooth note assigned dentist saves multiline note");
        }
        authenticate(RoleCodes.Admin);
        using (var response = await client.PostAsJsonAsync(path, new ToothConditionRequest(11, "O", "RESTORED", new string('x', 500))))
            check(response.StatusCode == HttpStatusCode.Created, "Tooth note Admin boundary500 accepted");
        using (var response = await client.PostAsJsonAsync(path, new ToothConditionRequest(51, null, "NORMAL", "  ")))
            check((await response.Content.ReadFromJsonAsync<ToothConditionResponse>())?.Note is null, "Tooth note whitespace normalizes null");
        using (var response = await client.PostAsJsonAsync(path, new ToothConditionRequest(85, null, "NORMAL")))
            check(response.StatusCode == HttpStatusCode.Created && (await response.Content.ReadFromJsonAsync<ToothConditionResponse>())?.Note is null, "Tooth note old payload without note remains compatible");
        authenticate(RoleCodes.Patient);
        using (var response = await client.GetAsync(path))
        {
            var rows = await response.Content.ReadFromJsonAsync<ToothConditionResponse[]>();
            check(response.StatusCode == HttpStatusCode.OK && rows?.Length == 4 && rows.Any(x => x.Note == body.Note!.Trim()), "Tooth note owner reads persisted notes");
        }
        var history = await client.GetFromJsonAsync<PagedResult<ToothHistoryItemResponse>>($"/api/patients/{patient.PatientId}/teeth/11/history");
        var visitHistory = history?.Items.Where(x => x.VisitId == visit.VisitId).ToArray();
        check(visitHistory?.Length == 2 && visitHistory.Any(x => x.ConditionCode == "CARIES" && x.Note == body.Note!.Trim()) &&
            visitHistory.Any(x => x.ConditionCode == "RESTORED" && x.Note?.Length == 500), "Tooth note append preserves original condition and note in history");
        var logs = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "ToothCondition" && a.Action == "MOD_FDI_CONDITION_CREATED").ToListAsync();
        check(logs.Count == 4 && logs.All(a => a.Detail == "{\"action\":\"condition_created\"}"), "Tooth note audit never contains medical text");
        authenticate(RoleCodes.Admin);
        var row = await db.Visits.SingleAsync(v => v.VisitId == visit.VisitId); row.IsLocked = true;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        using (var response = await client.PostAsJsonAsync(path, body)) check(response.StatusCode == HttpStatusCode.Conflict, "Tooth note locked visit 409");
        row = await db.Visits.SingleAsync(v => v.VisitId == visit.VisitId); row.IsLocked = false; row.DentistId = null;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); authenticate(RoleCodes.Dentist);
        using (var response = await client.PostAsJsonAsync(path, body)) check(response.StatusCode == HttpStatusCode.Forbidden, "Tooth note unrelated dentist 403");
        authenticate(RoleCodes.Patient);
        var otherVisit = await db.Visits.AsNoTracking().FirstAsync(v => v.PatientId == otherPatientId);
        using (var response = await client.GetAsync($"/api/visits/{otherVisit.VisitId}/tooth-conditions")) check(response.StatusCode == HttpStatusCode.NotFound, "Tooth note other patient 404");
        check(await db.ToothConditions.CountAsync(c => c.VisitId == visit.VisitId) == 4, "Tooth note rejected writes never add records");
    }
}
