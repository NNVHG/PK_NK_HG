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

internal static class QueueImagingHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        Action<string> authenticate, Action<string, int> authenticateActor, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Imaging fixtures require an owned isolated database.");
        await db.Database.OpenConnectionAsync();
        check(db.Database.GetDbConnection().ServerVersion.StartsWith("16."), "Imaging uses PostgreSQL16");
        await db.Database.CloseConnectionAsync();
        var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == RoleCodes.Dentist) ?? new Role { RoleCode = RoleCodes.Dentist, RoleName = "Test dentist" };
        var dentist = new User { Role = role, FullName = "Synthetic imaging dentist", IsActive = true,
            Phone = "IMG" + Guid.NewGuid().ToString("N")[..14], PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
        db.Users.Add(dentist); await db.SaveChangesAsync(); await TestChairFixtures.EnsureAsync(db, dentist.UserId, actorId);
        var today = new VietnamClock(SmokeTestTimeProvider.Instance).Today;
        async Task<QueueEntry> Entry(QueueStatus status = QueueStatus.InConsultation, bool booked = false)
        {
            var patient = new Patient { FullName = "Synthetic imaging patient", Phone = "0999999941", DateOfBirth = new(1990, 1, 1) };
            var row = new QueueEntry { Patient = patient, DentistId = dentist.UserId,
                Visit = new Visit { Patient = patient, DentistId = dentist.UserId, CreatedByUserId = actorId, Status = VisitStatuses.InProgress,
                    StartedAt = new DateTime(DateTime.UtcNow.AddMinutes(-20).Ticks / 10 * 10, DateTimeKind.Utc) },
                QueueDate = today, QueueNumber = (await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0) + 1,
                CheckInTime = DateTime.UtcNow, IsPriority = booked, Status = status };
            if (booked) row.Appointment = new Appointment { Patient = patient, DentistId = dentist.UserId, AppointmentDate = today,
                SlotTime = new(8, 0), CreatedByUserId = actorId, Status = AppointmentStatuses.CheckedIn };
            db.QueueEntries.Add(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return row;
        }
        var entry = await Entry(); var path = $"/api/queue/{entry.QueueEntryId}/status";
        Task<HttpResponseMessage> Put(int target, string? reason = "Synthetic imaging indication", int? dentistId = null)
            => client.PutAsJsonAsync(path, new { newStatus = target, reason, dentistId });
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await Put(5)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Imaging anonymous401");
        foreach (var denied in new[] { RoleCodes.Patient, RoleCodes.Receptionist, RoleCodes.Assistant })
        { authenticate(denied); using var r = await Put(5); check(r.StatusCode == HttpStatusCode.Forbidden, $"Imaging order {denied}403"); }
        authenticateActor(RoleCodes.Dentist, actorId);
        using (var r = await Put(5)) check(r.StatusCode == HttpStatusCode.Forbidden, "Foreign dentist cannot order imaging403");
        authenticateActor(RoleCodes.Dentist, dentist.UserId);
        using (var r = await Put(5, " ")) check(r.StatusCode == HttpStatusCode.BadRequest, "Missing indication400");
        using (var r = await Put(5, dentistId: dentist.UserId)) check(r.StatusCode == HttpStatusCode.BadRequest, "Imaging cannot reassign dentist400");
        using (var r = await Put(5)) check(r.StatusCode == HttpStatusCode.OK, "Assigned dentist orders imaging200");
        var persisted = await db.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == entry.QueueEntryId);
        check(persisted.Status == QueueStatus.InImaging && persisted.Visit!.Status == VisitStatuses.InProgress &&
            persisted.Visit.DentistId == dentist.UserId && persisted.Visit.StartedAt == entry.Visit!.StartedAt,
            "Imaging preserves same open Visit and dentist");
        check(await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == entry.QueueEntryId && h.Reason == "Synthetic imaging indication") == 1,
            "Imaging indication and actor history persisted");
        check(await db.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId == entry.QueueEntryId && a.UserId == dentist.UserId && !a.Detail!.Contains("Synthetic imaging indication")) == 1,
            "Imaging required audit committed without clinical indication");
        using (var r = await Put(5)) check(!r.IsSuccessStatusCode, "Duplicate imaging order rejected");
        foreach (var denied in new[] { RoleCodes.Dentist, RoleCodes.Receptionist, RoleCodes.Patient })
        { authenticate(denied); using var r = await Put(1, null); check(r.StatusCode == HttpStatusCode.Forbidden, $"Complete imaging {denied}403"); }
        authenticate(RoleCodes.Assistant);
        using (var r = await Put(1, null)) check(r.StatusCode == HttpStatusCode.OK, "Assistant completes imaging200");
        persisted = await db.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == entry.QueueEntryId);
        check(persisted.Status == QueueStatus.Waiting && persisted.IsPriority && persisted.DentistId == dentist.UserId &&
            persisted.Visit!.Status == VisitStatuses.InProgress && !persisted.Visit.IsLocked, "Imaging return keeps original Visit/dentist and sets priority");
        using (var r = await Put(1, null)) check(!r.IsSuccessStatusCode, "Duplicate imaging completion rejected");
        check(await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == entry.QueueEntryId) == 2 &&
            await db.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId == entry.QueueEntryId) == 2, "Exactly two committed transitions/history/audits");
        var booked = await Entry(QueueStatus.Waiting, true);
        // A newly booked, still unstarted Visit must not block resuming the imaging Visit.
        var bookedVisit = await db.Visits.SingleAsync(v => v.VisitId == booked.VisitId); bookedVisit.Status = VisitStatuses.Created; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var repo = new QueueRepository(db);
        var next = await repo.GetNextWaitingEntryAsync(today, dentist.UserId);
        check(next?.QueueEntryId == entry.QueueEntryId, "PostgreSQL Call Next ordering puts imaging return before 08:00 priority appointment");
        var page = await repo.GetTodayQueueAsync(today, QueueStatus.Waiting, dentist.UserId, 1, 100);
        check(page.Items[0].QueueEntryId == entry.QueueEntryId, "PostgreSQL paged internal queue puts imaging return first");
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync("/api/queue/display"))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("waiting")[0].GetProperty("queueEntryId").GetInt32() == entry.QueueEntryId,
                "Public monitor matches imaging return priority");
            check(!json.ToString().Contains("Synthetic imaging indication"), "Public monitor never exposes indication/history");
        }
        authenticateActor(RoleCodes.Dentist, dentist.UserId);
        using (var r = await client.PostAsJsonAsync("/api/queue/call-next", new { }))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("queueEntryId").GetInt32() == entry.QueueEntryId,
                "Call Next resumes original imaging Visit");
        }
        using (var r = await client.PutAsJsonAsync(path, new { newStatus = 2, dentistId = dentist.UserId }))
            check(r.StatusCode == HttpStatusCode.OK, "Imaging recall requires separate clinical start200");
        persisted = await db.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == entry.QueueEntryId);
        check(persisted.VisitId == entry.VisitId && persisted.Visit!.StartedAt == entry.Visit!.StartedAt, "Resume never creates a second Visit or resets start time");
        var failed = await Entry();
        var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;
        await using (var faultDb = new DentalDbContext(faultOptions))
        {
            var visits = new VisitRepository(faultDb); var patients = new PatientRepository(faultDb);
            var audit = new AuditLogger(faultDb, NullLogger<AuditLogger>.Instance); var clock = new VietnamClock(SmokeTestTimeProvider.Instance);
            var service = new QueueService(new QueueRepository(faultDb), new AppointmentRepository(faultDb), patients, visits, new InvoiceRepository(faultDb), new UserRepository(faultDb),
                new UnitOfWork(faultDb), audit, clock, new InvoiceDraftService(new InvoiceRepository(faultDb), visits, patients, audit, clock), TestChairFixtures.Service(faultDb, clock));
            var threw = false;
            try { await service.UpdateQueueStatusAsync(failed.QueueEntryId, dentist.UserId, RoleCodes.Dentist, new(5, Reason: "Synthetic rollback indication"), null); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic audit failure") { threw = true; }
            check(threw, "Required imaging audit failure propagates to rollback");
        }
        check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == failed.QueueEntryId && q.Status == QueueStatus.InConsultation) &&
            !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == failed.QueueEntryId) &&
            !await db.AuditLogs.AnyAsync(a => a.EntityType == "QueueEntry" && a.EntityId == failed.QueueEntryId),
            "Required audit failure rolls back queue/history/audit together");
        authenticate(RoleCodes.Admin);
        using (var r = await client.PutAsJsonAsync($"/api/queue/{failed.QueueEntryId}/status", new { newStatus = 5, reason = "Synthetic Admin indication" }))
            check(r.StatusCode == HttpStatusCode.OK, "Admin can order imaging");
        using (var r = await client.PutAsJsonAsync($"/api/queue/{failed.QueueEntryId}/status", new { newStatus = 1 }))
            check(r.StatusCode == HttpStatusCode.OK, "Admin can complete imaging");
        var race = await Entry();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PutAsJsonAsync(
            $"/api/queue/{race.QueueEntryId}/status", new { newStatus = 5, reason = "Synthetic concurrent indication" })));
        try
        {
            check(responses.Count(r => r.StatusCode == HttpStatusCode.OK) == 1, "Concurrent imaging requests commit exactly once");
            var rejected = responses.Single(r => r.StatusCode != HttpStatusCode.OK);
            var error = await rejected.Content.ReadFromJsonAsync<JsonElement>();
            check((rejected.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict) &&
                (error.GetProperty("code").GetString() is "CHK_003" or "CHK_010"), "Concurrent loser is a business conflict, never500");
            check(await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == race.QueueEntryId) == 1 &&
                await db.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId == race.QueueEntryId) == 1,
                "Concurrent imaging persists exactly one history and required audit");
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added)) throw new InvalidOperationException("Synthetic audit failure");
            return ValueTask.FromResult(result);
        }
    }
}
