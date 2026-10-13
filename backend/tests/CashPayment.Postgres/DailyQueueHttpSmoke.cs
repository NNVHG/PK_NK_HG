using System.Net;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class DailyQueueHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var today = new VietnamClock(TimeProvider.System).Today;
        var start = await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0;
        var rows = new List<QueueEntry>();
        for (var i = 0; i < 6; i++)
        {
            var patient = new Patient { FullName = $"Synthetic queue {i}", DateOfBirth = new(1990, 1, 1), Phone = $"099999996{i}" };
            var row = new QueueEntry { Patient = patient, QueueDate = i == 5 ? today.AddDays(-1) : today, QueueNumber = start + i + 1,
                IsPriority = i < 2, CheckInTime = DateTime.UtcNow.AddMinutes(-i), Status = i == 4 ? QueueStatus.Completed : QueueStatus.Waiting };
            if (i < 2) row.Appointment = new Appointment { Patient = patient, AppointmentDate = today, SlotTime = new(9 - i, 0), CreatedByUserId = actorId, Status = AppointmentStatuses.CheckedIn };
            db.QueueEntries.Add(row); rows.Add(row);
        }
        await db.SaveChangesAsync();
        var audits = await db.AuditLogs.CountAsync();
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync("/api/queue")) check(response.StatusCode == HttpStatusCode.Unauthorized, "Queue list anonymous 401");
        authenticate(RoleCodes.Patient);
        using (var response = await client.GetAsync("/api/queue")) check(response.StatusCode == HttpStatusCode.Forbidden, "Queue list Patient 403");
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role);
            using var response = await client.GetAsync("/api/queue?status=1&pageSize=100");
            check(response.StatusCode == HttpStatusCode.OK, $"Queue list {role} 200");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var ids = json.RootElement.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("queueEntryId").GetInt32()).Where(id => rows.Any(r => r.QueueEntryId == id)).ToArray();
            check(ids.SequenceEqual(new[] { rows[1].QueueEntryId, rows[0].QueueEntryId, rows[3].QueueEntryId, rows[2].QueueEntryId }), $"Queue {role} priority by slot then ordinary by check-in, today/status filter");
            check(response.Headers.CacheControl?.NoStore == true, $"Queue {role} no-store");
        }
        using (var response = await client.GetAsync("/api/queue?status=1&pageSize=1"))
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(response.StatusCode == HttpStatusCode.OK && json.RootElement.GetProperty("items").GetArrayLength() == 1 && json.RootElement.GetProperty("hasNextPage").GetBoolean(), "Queue bounded pagination");
        }
        foreach (var query in new[] { "status=6", "dentistId=0", "page=1000001", "pageSize=101" })
        { using var response = await client.GetAsync($"/api/queue?{query}"); check(response.StatusCode == HttpStatusCode.BadRequest, $"Queue invalid {query} 400"); }
        check(await db.AuditLogs.CountAsync() == audits && await db.QueueEntries.CountAsync(q => rows.Select(r => r.QueueEntryId).Contains(q.QueueEntryId)) == 6, "Queue GET never changes queue or audit");
    }
}
