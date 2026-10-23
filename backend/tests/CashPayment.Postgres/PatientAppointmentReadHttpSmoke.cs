using System.Net;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class PatientAppointmentReadHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var profiles = await db.Patients.Where(p => p.UserId == actorId && p.IsActive).OrderBy(p => p.PatientId).Take(1).ToArrayAsync();
        var other = new Patient { FullName = "Synthetic other owner", DateOfBirth = new(1990, 1, 1), Phone = "0999999931" };
        db.Patients.Add(other); await db.SaveChangesAsync();
        var date = new VietnamClock(SmokeTestTimeProvider.Instance).Today.AddDays(10);
        var owned = profiles.Select((p, i) => new Appointment { PatientId = p.PatientId, AppointmentDate = date, SlotTime = new(8 + i, 0), CreatedByUserId = actorId, Status = i == 0 ? AppointmentStatuses.Scheduled : AppointmentStatuses.Cancelled }).ToArray();
        var unowned = new Appointment { PatientId = other.PatientId, AppointmentDate = date, SlotTime = new(8, 0), CreatedByUserId = actorId, Status = AppointmentStatuses.Scheduled };
        db.Appointments.AddRange(owned.Append(unowned)); await db.SaveChangesAsync();
        var auditCount = await db.AuditLogs.CountAsync(); var rowCount = await db.Appointments.CountAsync();
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync("/api/appointments")) check(response.StatusCode == HttpStatusCode.Unauthorized, "Patient appointment list anonymous 401");
        authenticate(RoleCodes.Patient);
        using (var response = await client.GetAsync($"/api/appointments?date={date:yyyy-MM-dd}&pageSize=1"))
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(response.StatusCode == HttpStatusCode.OK && json.RootElement.GetProperty("totalCount").GetInt32() == 2 && json.RootElement.GetProperty("items").GetArrayLength() == 1, "Patient ownership applies before count and pagination");
            check(response.Headers.CacheControl?.NoStore == true, "Patient appointment list no-store");
        }
        var ids = new List<int>();
        for (var page = 1; page <= 2; page++)
        {
            using var response = await client.GetAsync($"/api/appointments?date={date:yyyy-MM-dd}&pageSize=1&page={page}");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            ids.Add(json.RootElement.GetProperty("items")[0].GetProperty("appointmentId").GetInt32());
        }
        check(ids.SequenceEqual(owned.Select(a => a.AppointmentId)), "Patient pagination includes own profile and excludes other account");
        using (var response = await client.GetAsync($"/api/appointments?date={date:yyyy-MM-dd}&ownerUserId=2147483647"))
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(json.RootElement.GetProperty("totalCount").GetInt32() == 2, "Patient cannot override JWT owner through query parameters");
        }
        using (var response = await client.GetAsync($"/api/appointments?patientId={other.PatientId}")) check(response.StatusCode == HttpStatusCode.NotFound, "Patient unrelated profile filter 404");
        using (var response = await client.GetAsync($"/api/appointments/{unowned.AppointmentId}")) check(response.StatusCode == HttpStatusCode.NotFound, "Patient unrelated appointment detail 404");
        using (var response = await client.GetAsync($"/api/appointments/{owned[0].AppointmentId}")) check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true, "Patient owned detail 200/no-store");
        using (var response = await client.GetAsync("/api/appointments/2147483647")) check(response.StatusCode == HttpStatusCode.NotFound, "Patient missing appointment detail 404");
        using (var response = await client.GetAsync($"/api/appointments?date={date:yyyy-MM-dd}&status=Cancelled&patientId={profiles[1].PatientId}"))
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(response.StatusCode == HttpStatusCode.OK && json.RootElement.GetProperty("totalCount").GetInt32() == 1, "Patient status and patient filter intersect owner scope");
        }
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role);
            using var response = await client.GetAsync($"/api/appointments?date={date:yyyy-MM-dd}");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(response.StatusCode == HttpStatusCode.OK && json.RootElement.GetProperty("totalCount").GetInt32() == 3, $"Appointment {role} read scope unchanged");
        }
        check(await db.AuditLogs.CountAsync() == auditCount && await db.Appointments.CountAsync() == rowCount, "Appointment read leaves records and audit unchanged");
    }
}
