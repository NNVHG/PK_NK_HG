using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

internal static class QueueLocationHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        int patientId, Action<string, int> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Owned test database required");
        async Task<User> Staff(string code)
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == code);
            if (role is null) { role = new() { RoleCode = code, RoleName = code }; db.Roles.Add(role); await db.SaveChangesAsync(); }
            var user = new User { RoleId = role.RoleId, FullName = "Synthetic location staff", IsActive = true,
                Phone = "QL" + Guid.NewGuid().ToString("N")[..14], PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
            db.Users.Add(user); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return user;
        }
        async Task<DentalChair> Locate(User dentist, string room, DentalChairStatus status = DentalChairStatus.Available)
        {
            var chair = new DentalChair { ChairNumber = "QL-" + Guid.NewGuid().ToString("N"), RoomName = room,
                Status = status, IsActive = status != DentalChairStatus.Inactive, CreatedByUserId = actorId };
            db.DentalChairs.Add(chair); await db.SaveChangesAsync();
            db.StaffWorkspaces.Add(new() { UserId = dentist.UserId, ChairId = chair.Id, UpdatedByUserId = actorId });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return chair;
        }
        var doctorA = await Staff(RoleCodes.Dentist); var doctorB = await Staff(RoleCodes.Dentist);
        var assistant = await Staff(RoleCodes.Assistant); var chairA = await Locate(doctorA, "Phòng 3"); var chairB = await Locate(doctorB, "Phòng 4");
        db.StaffWorkspaces.Add(new() { UserId = assistant.UserId, DentistId = doctorA.UserId, UpdatedByUserId = actorId });
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time"));
        var row = new QueueEntry { PatientId = patientId, QueueDate = today, QueueNumber = 8901, DentistId = doctorB.UserId,
            Status = QueueStatus.Waiting, CheckInTime = DateTime.UtcNow, Notes = "synthetic private location note",
            Visit = new Visit { PatientId = patientId, CreatedByUserId = actorId, Status = VisitStatuses.Created, Diagnosis = "synthetic private diagnosis" } };
        db.QueueEntries.Add(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear();

        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync("/api/queue/call-dentists")) check(r.StatusCode == HttpStatusCode.Unauthorized, "Directory remains JWT-only401");
        authenticate(RoleCodes.Receptionist, actorId);
        using (var r = await client.GetAsync("/api/queue/call-dentists")) check(r.StatusCode == HttpStatusCode.Forbidden, "Receptionist cannot read calling directory403");
        authenticate(RoleCodes.Assistant, assistant.UserId);
        using (var r = await client.GetAsync("/api/queue/call-dentists"))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>(); var d = json.EnumerateArray().Single(x => x.GetProperty("dentistId").GetInt32() == doctorB.UserId);
            check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true && d.GetProperty("roomName").GetString() == "Phòng 4", "Directory exposes destination room with NoStore");
            check(d.EnumerateObject().Select(p => p.Name).OrderBy(x => x).SequenceEqual(new[] { "chairNumber", "dentistId", "dentistName", "roomName" }), "Directory keeps four-field privacy whitelist");
        }
        using (var r = await client.PostAsJsonAsync("/api/queue/call-next", new { dentistId = doctorB.UserId }))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("status").GetInt32() == 6 && json.GetProperty("roomName").GetString() == "Phòng 4" &&
                json.GetProperty("chairNumber").GetString() == chairB.ChairNumber, "Existing DL258 invitation uses destination B room, not calling assistant A room");
        }
        check(await db.StaffWorkspaces.AsNoTracking().AnyAsync(w => w.UserId == assistant.UserId && w.DentistId == doctorA.UserId && w.ChairId == null && w.Version == 1), "Calling does not change assistant pairing/version");
        check(await db.StaffWorkspaces.AsNoTracking().AnyAsync(w => w.UserId == doctorA.UserId && w.ChairId == chairA.Id && w.Version == 1), "Calling does not move paired dentist A");
        check(await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == row.VisitId && v.Status == VisitStatuses.Created && v.DentistId == null && v.StartedAt == null), "Location read/calling never starts or reassigns clinical Visit");
        check(await db.DentalChairs.AsNoTracking().AllAsync(c => c.Status == DentalChairStatus.Available), "Invitation does not occupy chairs");
        using (var r = await client.GetAsync($"/api/queue/{row.QueueEntryId}"))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("entry").GetProperty("roomName").GetString() == "Phòng 4", "Internal detail displays actual room");
        }
        using (var r = await client.GetAsync("/api/queue?status=6"))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("items").EnumerateArray().Single().GetProperty("roomName").GetString() == "Phòng 4", "Internal queue page displays actual room");
        }
        client.DefaultRequestHeaders.Authorization = null;
        foreach (var path in new[] { "/api/queue/display", "/api/queue/public-monitor" })
        {
            using var r = await client.GetAsync(path); var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            var item = json.GetProperty("inConsultation").EnumerateArray().Single();
            check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true && item.GetProperty("roomName").GetString() == "Phòng 4", "Anonymous TV aliases display destination room with NoStore");
            check(item.EnumerateObject().Select(p => p.Name).OrderBy(x => x).SequenceEqual(new[] { "chairNumber", "dentistName", "isPriority", "maskedPatientName", "queueEntryId", "queueNumber", "queueNumberDisplay", "roomName", "status", "statusText" }), "TV keeps exact minimal whitelist, no contact/diagnosis/workspace IDs");
        }
        foreach (var status in new[] { DentalChairStatus.Available, DentalChairStatus.Occupied, DentalChairStatus.Maintenance, DentalChairStatus.Inactive })
        {
            var storedChair = await db.DentalChairs.FindAsync(chairB.Id); storedChair!.Status = status; storedChair.IsActive = status != DentalChairStatus.Inactive;
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var locations = await new QueueRepository(db).GetDentistLocationsAsync(new[] { doctorB.UserId });
            check(locations.Count == (status is DentalChairStatus.Available or DentalChairStatus.Occupied ? 1 : 0), $"Location eligibility respects {status}");
        }
        var restored = await db.DentalChairs.FindAsync(chairB.Id); restored!.Status = DentalChairStatus.Available; restored.IsActive = true;
        var disabled = await db.Users.FindAsync(doctorB.UserId); disabled!.IsActive = false; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        check((await new QueueRepository(db).GetDentistLocationsAsync(new[] { doctorB.UserId })).Count == 0, "Inactive dentist never supplies a destination room");
        using (var r = await client.GetAsync("/api/queue/display"))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>(); var item = json.GetProperty("inConsultation").EnumerateArray().Single();
            check(item.GetProperty("roomName").ValueKind == JsonValueKind.Null && item.GetProperty("chairNumber").ValueKind == JsonValueKind.Null, "TV clears unavailable room instead of retaining/inventing destination");
        }
        disabled = await db.Users.FindAsync(doctorB.UserId); disabled!.IsActive = true; disabled.RoleId = assistant.RoleId;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        check((await new QueueRepository(db).GetDentistLocationsAsync(new[] { doctorB.UserId })).Count == 0, "Role-changed staff never supplies dentist location");
        check((await new QueueRepository(db).GetDentistLocationsAsync(Array.Empty<int>())).Count == 0, "Empty dentist set returns no fabricated location");
        check(await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == row.QueueEntryId) == 1 &&
            await db.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId == row.QueueEntryId) == 1, "Location reads create no queue transitions or audits");
    }
}
