using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Features.Queue.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

internal static class QueueCallingHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        Action<string> authenticate, Action<string, int> authenticateActor, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Owned test database required");
        var today = new VietnamClock(SmokeTestTimeProvider.Instance).Today;
        async Task<User> Staff(string code)
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == code);
            if (role is null) { role = new Role { RoleCode = code, RoleName = "Synthetic " + code }; db.Roles.Add(role); await db.SaveChangesAsync(); }
            var user = new User { RoleId = role.RoleId, FullName = "Synthetic calling staff", IsActive = true,
                Phone = "CALL" + Guid.NewGuid().ToString("N")[..14], PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
            db.Users.Add(user); await db.SaveChangesAsync(); if (code == RoleCodes.Dentist) await TestChairFixtures.EnsureAsync(db, user.UserId, actorId); return user;
        }
        async Task<QueueEntry> Waiting(int? dentistId, bool priority = false)
        {
            var patient = new Patient { FullName = "Synthetic calling patient", DateOfBirth = new(1990, 1, 1), Phone = "0999999943" };
            var entry = new QueueEntry { Patient = patient, DentistId = dentistId, QueueDate = today,
                QueueNumber = (await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0) + 1, CheckInTime = DateTime.UtcNow,
                Status = QueueStatus.Waiting, IsPriority = priority, Visit = new Visit { Patient = patient, CreatedByUserId = actorId, Status = VisitStatuses.Created } };
            db.QueueEntries.Add(entry); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return entry;
        }
        var doctor = await Staff(RoleCodes.Dentist); var other = await Staff(RoleCodes.Dentist); var assistant = await Staff(RoleCodes.Assistant);
        var unassigned = await Waiting(null, true); var assigned = await Waiting(doctor.UserId);
        const string directory = "/api/queue/call-dentists"; const string call = "/api/queue/call-next";
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync(directory)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Calling directory anonymous401");
        using (var r = await client.PostAsJsonAsync(call, new { dentistId = doctor.UserId })) check(r.StatusCode == HttpStatusCode.Unauthorized, "Calling anonymous401");
        foreach (var role in new[] { RoleCodes.Receptionist, RoleCodes.Patient })
        {
            authenticate(role);
            using (var r = await client.GetAsync(directory)) check(r.StatusCode == HttpStatusCode.Forbidden, $"Calling directory {role}403");
            using (var r = await client.PostAsJsonAsync(call, new { dentistId = doctor.UserId })) check(r.StatusCode == HttpStatusCode.Forbidden, $"Calling {role}403");
        }
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role); using var r = await client.GetAsync(directory); var items = await r.Content.ReadFromJsonAsync<JsonElement>();
            check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true, $"Calling directory {role}200/no-store");
            check(items.EnumerateArray().All(x => x.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "chairNumber", "dentistId", "dentistName", "roomName" })),
                $"Calling directory {role} contains only dentist id/name/room/chair");
        }
        authenticateActor(RoleCodes.Assistant, assistant.UserId);
        using (var r = await client.PostAsJsonAsync(call, new { })) check(r.StatusCode == HttpStatusCode.BadRequest, "Assistant must select dentist400");
        using (var r = await client.PostAsJsonAsync(call, new { dentistId = doctor.UserId }))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("queueEntryId").GetInt32() == assigned.QueueEntryId && json.GetProperty("status").GetInt32() == 6,
                "Assistant invites assigned entry, skipping earlier unassigned entry");
        }
        var persisted = await db.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == assigned.QueueEntryId);
        check(persisted.Status == QueueStatus.Calling && persisted.DentistId == doctor.UserId && persisted.Visit!.Status == VisitStatuses.Created &&
            persisted.Visit.DentistId == null && persisted.Visit.StartedAt == null, "Assistant Calling cannot start or assign clinical Visit");
        check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == unassigned.QueueEntryId && q.Status == QueueStatus.Waiting && q.DentistId == null),
            "Assistant does not claim unassigned patient");
        check(await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == assigned.QueueEntryId && h.FromStatus == QueueStatus.Waiting && h.ToStatus == QueueStatus.Calling && h.ChangedByUserId == assistant.UserId),
            "Calling history records actual Assistant actor");
        using (var r = await client.PostAsJsonAsync(call, new { dentistId = doctor.UserId }))
        { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.StatusCode == HttpStatusCode.Conflict && json.GetProperty("code").GetString() == "CHK_013", "Second Calling for dentist blocked409/CHK013"); }
        var path = $"/api/queue/{assigned.QueueEntryId}/status";
        using (var r = await client.PutAsJsonAsync(path, new { newStatus = 2, dentistId = doctor.UserId })) check(r.StatusCode == HttpStatusCode.Forbidden, "Assistant cannot start Calling clinical Visit403");
        using (var r = await client.PutAsJsonAsync(path, new { newStatus = 6 })) check(r.StatusCode == HttpStatusCode.BadRequest, "Generic status cannot bypass calling operation400");
        authenticateActor(RoleCodes.Dentist, other.UserId);
        using (var r = await client.PutAsJsonAsync(path, new { newStatus = 2 })) check(r.StatusCode == HttpStatusCode.Forbidden, "Foreign dentist cannot start invited patient403");
        authenticateActor(RoleCodes.Dentist, doctor.UserId);
        using (var r = await client.PutAsJsonAsync(path, new { newStatus = 2 })) check(r.StatusCode == HttpStatusCode.OK, "Assigned dentist separately starts Calling200");
        persisted = await db.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == assigned.QueueEntryId);
        check(persisted.Status == QueueStatus.InConsultation && persisted.Visit!.Status == VisitStatuses.InProgress && persisted.Visit.DentistId == doctor.UserId && persisted.Visit.StartedAt.HasValue,
            "Explicit clinical start updates Queue and Visit together");
        check(await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == assigned.QueueEntryId) == 2 &&
            await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == assigned.QueueEntryId && h.FromStatus == QueueStatus.Calling && h.ToStatus == QueueStatus.InConsultation) &&
            await db.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId == assigned.QueueEntryId) == 2,
            "Calling and clinical start have separate complete histories/audits");
        using (var r = await client.PostAsJsonAsync(call, new { })) check(r.StatusCode == HttpStatusCode.Conflict, "Active Visit still blocks another patient409");
        authenticateActor(RoleCodes.Dentist, other.UserId);
        using (var r = await client.PostAsJsonAsync(call, new { }))
        { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("queueEntryId").GetInt32() == unassigned.QueueEntryId && json.GetProperty("status").GetInt32() == 6, "Dentist can invite unassigned patient without starting Visit"); }
        var repository = new QueueRepository(db);
        check((await repository.GetOpenQueueEntriesForPatientAsync(unassigned.PatientId)).Any(q => q.Status == QueueStatus.Calling), "Calling counts as open patient queue entry");
        using (var r = await client.GetAsync("/api/queue?status=6"))
        { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("items").EnumerateArray().All(x => x.GetProperty("status").GetInt32() == 6), "Calling is queryable in internal queue"); }
        client.DefaultRequestHeaders.Authorization = null;
        foreach (var publicPath in new[] { "/api/queue/display", "/api/queue/public-monitor" })
        {
            using var r = await client.GetAsync(publicPath); var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            var current = json.GetProperty("inConsultation").EnumerateArray().ToArray();
            check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true && current[0].GetProperty("status").GetInt32() == 6, "Public monitor shows Calling first, anonymous/no-store");
            check(current.All(x => !x.TryGetProperty("patientCode", out _) && !x.TryGetProperty("patientId", out _) && !x.TryGetProperty("patientPhone", out _) &&
                !x.TryGetProperty("diagnosis", out _) && !x.TryGetProperty("notes", out _)), "Public Calling DTO excludes identity/contact/clinical data");
        }
        var adminDoctor = await Staff(RoleCodes.Dentist); var adminEntry = await Waiting(adminDoctor.UserId); authenticate(RoleCodes.Admin);
        using (var r = await client.PostAsJsonAsync(call, new { dentistId = adminDoctor.UserId })) check(r.StatusCode == HttpStatusCode.OK, "Admin can invite Calling");
        check(await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == adminEntry.VisitId && v.Status == VisitStatuses.Created && v.StartedAt == null), "Admin invitation also leaves clinical Visit unstarted");
        var faultDoctor = await Staff(RoleCodes.Dentist); var fault = await Waiting(faultDoctor.UserId);
        var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;
        QueueService Service(DentalDbContext context)
        {
            var visits = new VisitRepository(context); var patients = new PatientRepository(context);
            var audit = new AuditLogger(context, NullLogger<AuditLogger>.Instance); var clock = new VietnamClock(SmokeTestTimeProvider.Instance);
            return new(new QueueRepository(context), new AppointmentRepository(context), patients, visits, new InvoiceRepository(context), new UserRepository(context),
                new UnitOfWork(context), audit, clock, new InvoiceDraftService(new InvoiceRepository(context), visits, patients, audit, clock), TestChairFixtures.Service(context, clock));
        }
        await using (var faultDb = new DentalDbContext(faultOptions))
        {
            var threw = false;
            try { await Service(faultDb).CallNextAsync(faultDoctor.UserId, RoleCodes.Dentist, new(null), null); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic Calling audit failure") { threw = true; }
            check(threw, "Calling required audit failure propagates");
        }
        check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == fault.QueueEntryId && q.Status == QueueStatus.Waiting) &&
            !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == fault.QueueEntryId) && !await db.AuditLogs.AnyAsync(a => a.EntityType == "QueueEntry" && a.EntityId == fault.QueueEntryId),
            "Calling audit failure rolls back queue/history/audit");
        using (var r = await client.PostAsJsonAsync(call, new { dentistId = faultDoctor.UserId })) check(r.StatusCode == HttpStatusCode.OK, "Calling can be retried after full rollback");
        await using (var faultDb = new DentalDbContext(faultOptions))
        {
            var threw = false;
            try { await Service(faultDb).UpdateQueueStatusAsync(fault.QueueEntryId, faultDoctor.UserId, RoleCodes.Dentist, new(2), null); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic Calling audit failure") { threw = true; }
            check(threw, "Starting Calling required audit failure propagates");
        }
        persisted = await db.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == fault.QueueEntryId);
        check(persisted.Status == QueueStatus.Calling && persisted.Visit!.Status == VisitStatuses.Created && persisted.Visit.StartedAt == null &&
            await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == fault.QueueEntryId) == 1 && await db.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId == fault.QueueEntryId) == 1,
            "Failed clinical start preserves prior Calling and never partially starts Visit");
    }
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added)) throw new InvalidOperationException("Synthetic Calling audit failure");
            return ValueTask.FromResult(result);
        }
    }
}
