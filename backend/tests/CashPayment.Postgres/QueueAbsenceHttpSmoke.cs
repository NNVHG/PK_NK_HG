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

internal static class QueueAbsenceHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        Action<string> authenticate, Action<string, int> authenticateActor, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Owned test database required");
        var today = new VietnamClock(TimeProvider.System).Today;
        async Task<User> Staff(string code)
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == code);
            if (role is null) { role = new Role { RoleCode = code, RoleName = "Synthetic " + code }; db.Roles.Add(role); await db.SaveChangesAsync(); }
            var user = new User { RoleId = role.RoleId, FullName = "Synthetic calling staff", IsActive = true,
                Phone = "CALL" + Guid.NewGuid().ToString("N")[..14], PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
            db.Users.Add(user); await db.SaveChangesAsync(); return user;
        }
        async Task<QueueEntry> Waiting(int? dentistId, bool priority = false)
        {
            var patient = new Patient { FullName = "Synthetic calling patient", DateOfBirth = new(1990, 1, 1), Phone = "0999999943" };
            var entry = new QueueEntry { Patient = patient, DentistId = dentistId, QueueDate = today,
                QueueNumber = (await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0) + 1, CheckInTime = DateTime.UtcNow,
                Status = QueueStatus.Waiting, IsPriority = priority, Visit = new Visit { Patient = patient, CreatedByUserId = actorId, Status = VisitStatuses.Created } };
            db.QueueEntries.Add(entry); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return entry;
        }
        var doctor = await Staff(RoleCodes.Dentist); var other = await Staff(RoleCodes.Dentist);
        var selected = await Waiting(doctor.UserId);
        async Task Calling(QueueEntry entry)
        {
            var tracked = await db.QueueEntries.SingleAsync(q => q.QueueEntryId == entry.QueueEntryId);
            tracked.Status = QueueStatus.Calling; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        await Calling(selected);
        var skip = $"/api/queue/{selected.QueueEntryId}/skip"; var recall = $"/api/queue/{selected.QueueEntryId}/recall";
        var payload = new { confirmed = true, reason = "Synthetic absence note" };
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.PostAsJsonAsync(skip, payload)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Skip anonymous401");
        using (var r = await client.PostAsJsonAsync(recall, payload)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Recall anonymous401");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Assistant })
        {
            authenticate(role);
            using (var r = await client.PostAsJsonAsync(skip, payload)) check(r.StatusCode == HttpStatusCode.Forbidden, $"Skip {role}403");
            using (var r = await client.PostAsJsonAsync(recall, payload)) check(r.StatusCode == HttpStatusCode.Forbidden, $"Recall {role}403");
        }
        authenticateActor(RoleCodes.Dentist, other.UserId);
        using (var r = await client.PostAsJsonAsync(skip, payload)) check(r.StatusCode == HttpStatusCode.Forbidden, "Foreign dentist cannot skip403");
        authenticateActor(RoleCodes.Dentist, doctor.UserId);
        using (var r = await client.PostAsJsonAsync(recall, payload)) check(r.StatusCode == HttpStatusCode.Forbidden, "Dentist cannot recall403");
        authenticate(RoleCodes.Receptionist);
        using (var r = await client.PostAsJsonAsync(skip, new { confirmed = false })) check(r.StatusCode == HttpStatusCode.BadRequest, "Skip requires confirmation400");
        using (var r = await client.PostAsJsonAsync(skip, new { confirmed = true, reason = new string('x', 501) })) check(r.StatusCode == HttpStatusCode.BadRequest, "Skip rejects oversized note400");
        using (var r = await client.PostAsJsonAsync(skip, payload)) check(r.StatusCode == HttpStatusCode.OK, "Receptionist marks absent200");
        var persisted = await db.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == selected.QueueEntryId);
        check(persisted.Status == QueueStatus.Skipped && persisted.DentistId == doctor.UserId && persisted.VisitId == selected.VisitId &&
            persisted.Visit!.Status == VisitStatuses.Created && persisted.Visit.StartedAt == null && persisted.Visit.EndedAt == null, "Absence preserves open Visit, dentist and number");
        check(await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == selected.QueueEntryId && h.FromStatus == QueueStatus.Calling && h.ToStatus == QueueStatus.Skipped && h.ChangedByUserId == actorId), "Skip history has real actor and transition");
        var repo = new QueueRepository(db);
        check((await repo.GetOpenQueueEntriesForPatientAsync(selected.PatientId)).Any(q => q.Status == QueueStatus.Skipped), "Skipped counts as open, prevents new check-in");
        using (var r = await client.PostAsJsonAsync("/api/queue/check-in", new { patientId = selected.PatientId, dentistId = doctor.UserId })) check(r.StatusCode == HttpStatusCode.Conflict, "Skipped patient cannot check-in again409");
        using (var r = await client.GetAsync("/api/queue?status=7")) { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("items").EnumerateArray().Any(q => q.GetProperty("queueEntryId").GetInt32() == selected.QueueEntryId), "Skipped filter supported"); }
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.GetAsync("/api/queue/display")) { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.Headers.CacheControl?.NoStore == true && !json.GetProperty("waiting").EnumerateArray().Concat(json.GetProperty("inConsultation").EnumerateArray()).Any(q => q.GetProperty("queueEntryId").GetInt32() == selected.QueueEntryId), "Skipped absent from anonymous monitor"); }
        var next = await Waiting(doctor.UserId, true);
        authenticateActor(RoleCodes.Dentist, doctor.UserId);
        using (var r = await client.PostAsJsonAsync("/api/queue/call-next", new { })) { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("queueEntryId").GetInt32() == next.QueueEntryId, "Skipping releases invitation for next patient"); }
        authenticate(RoleCodes.Admin);
        using (var r = await client.PostAsJsonAsync(recall, payload)) check(r.StatusCode == HttpStatusCode.OK, "Admin restores Skipped to Waiting200");
        check((await repo.GetNextWaitingEntryAsync(today, doctor.UserId))?.QueueEntryId == selected.QueueEntryId, "Recall precedes ordinary priority waiting in repository");
        using (var r = await client.GetAsync("/api/queue?status=1")) { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(json.GetProperty("items")[0].GetProperty("queueEntryId").GetInt32() == selected.QueueEntryId, "Internal list uses recall priority"); }
        using (var r = await client.GetAsync("/api/queue/display")) { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(json.GetProperty("waiting")[0].GetProperty("queueEntryId").GetInt32() == selected.QueueEntryId, "Public monitor uses same recall priority"); }
        using (var r = await client.PostAsJsonAsync(recall, payload)) check(r.StatusCode == HttpStatusCode.Conflict, "Duplicate recall stale409");
        using (var r = await client.PutAsJsonAsync($"/api/queue/{selected.QueueEntryId}/status", new { newStatus = 7 })) check(r.StatusCode == HttpStatusCode.BadRequest, "Generic status cannot bypass skip400");
        authenticateActor(RoleCodes.Dentist, doctor.UserId);
        using (var r = await client.PostAsJsonAsync("/api/queue/call-next", new { })) check(r.StatusCode == HttpStatusCode.Conflict, "Recall does not cancel existing invitation");
        authenticate(RoleCodes.Receptionist);
        using (var r = await client.PostAsJsonAsync($"/api/queue/{next.QueueEntryId}/skip", payload)) check(r.StatusCode == HttpStatusCode.OK, "Second invitation can be marked absent");
        authenticateActor(RoleCodes.Dentist, doctor.UserId);
        using (var r = await client.PostAsJsonAsync("/api/queue/call-next", new { })) check(r.StatusCode == HttpStatusCode.OK, "Recalled patient can be invited again");
        using (var r = await client.PutAsJsonAsync($"/api/queue/{selected.QueueEntryId}/status", new { newStatus = 2 })) check(r.StatusCode == HttpStatusCode.OK, "Recalled patient separately starts clinical Visit");
        using (var r = await client.PostAsJsonAsync(skip, payload)) check(r.StatusCode == HttpStatusCode.Conflict, "Started Visit cannot skip409");
        // Simulate the existing imaging-return invitation; Q-303 explicitly forbids skipping it.
        await Calling(selected);
        using (var r = await client.GetAsync($"/api/queue/{selected.QueueEntryId}")) { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(!json.GetProperty("entry").GetProperty("canMarkAbsent").GetBoolean(), "Started imaging-return invitation has no skip eligibility"); }
        using (var r = await client.PostAsJsonAsync(skip, payload)) check(r.StatusCode == HttpStatusCode.Conflict, "Calling after clinical start cannot skip409");
        var imaging = await Waiting(doctor.UserId);
        db.QueueStatusHistories.Add(new QueueStatusHistory { QueueEntryId = imaging.QueueEntryId, FromStatus = QueueStatus.InImaging, ToStatus = QueueStatus.Waiting, ChangedByUserId = actorId, ChangedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        authenticate(RoleCodes.Receptionist);
        using (var r = await client.PostAsJsonAsync($"/api/queue/{next.QueueEntryId}/recall", payload)) check(r.StatusCode == HttpStatusCode.OK, "Receptionist can recall200");
        check((await repo.GetNextWaitingEntryAsync(today, doctor.UserId))?.QueueEntryId == imaging.QueueEntryId, "Imaging retains highest priority above recall DL053");
        var faultDoctor = await Staff(RoleCodes.Dentist); var fault = await Waiting(faultDoctor.UserId); await Calling(fault);
        var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;
        QueueService Service(DentalDbContext context)
        {
            var visits = new VisitRepository(context); var patients = new PatientRepository(context);
            var audit = new AuditLogger(context, NullLogger<AuditLogger>.Instance); var clock = new VietnamClock(TimeProvider.System);
            return new(new QueueRepository(context), new AppointmentRepository(context), patients, visits, new UserRepository(context),
                new UnitOfWork(context), audit, clock, new InvoiceDraftService(new InvoiceRepository(context), visits, patients, audit, clock));
        }
        foreach (var recalling in new[] { false, true })
        {
            if (recalling) { var tracked = await db.QueueEntries.SingleAsync(q => q.QueueEntryId == fault.QueueEntryId); tracked.Status = QueueStatus.Skipped; await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
            await using var faultDb = new DentalDbContext(faultOptions); var threw = false;
            try { await Service(faultDb).ChangeAbsenceAsync(fault.QueueEntryId, actorId, RoleCodes.Receptionist, recalling, new(true), null); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic absence audit failure") { threw = true; }
            check(threw, $"{(recalling ? "Recall" : "Skip")} required audit failure propagates");
            check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == fault.QueueEntryId && q.Status == (recalling ? QueueStatus.Skipped : QueueStatus.Calling)) &&
                !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == fault.QueueEntryId) && !await db.AuditLogs.AnyAsync(a => a.EntityType == "QueueEntry" && a.EntityId == fault.QueueEntryId), "Audit failure rolls back queue/history/audit");
        }
        // Concurrent skip/start and two simultaneous recalls: only one transaction may commit.
        foreach (var recalling in new[] { false, true })
        {
            var raceDoctor = await Staff(RoleCodes.Dentist); var race = await Waiting(raceDoctor.UserId); await Calling(race);
            if (recalling) { var tracked = await db.QueueEntries.SingleAsync(q => q.QueueEntryId == race.QueueEntryId); tracked.Status = QueueStatus.Skipped; await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
            var raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
            async Task<Result<QueueEntryResponse>> Op(bool second)
            {
                await using var context = new DentalDbContext(raceOptions); var service = Service(context);
                return second && !recalling ? await service.UpdateQueueStatusAsync(race.QueueEntryId, raceDoctor.UserId, RoleCodes.Dentist, new(2), null)
                    : await service.ChangeAbsenceAsync(race.QueueEntryId, actorId, RoleCodes.Receptionist, recalling, new(true), null);
            }
            var results = await Task.WhenAll(Op(false), Op(true));
            check(results.Count(r => r.IsSuccess) == 1 && results.Count(r => r.Error.Code == "CHK_010") == 1, recalling ? "Concurrent recall commits once" : "Skip versus clinical start commits once");
            check(await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == race.QueueEntryId) == 1 && await db.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId == race.QueueEntryId) == 1, "Race commits single history/audit");
            persisted = await db.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == race.QueueEntryId);
            check((persisted.Status == QueueStatus.InConsultation && persisted.Visit!.Status == VisitStatuses.InProgress) ||
                (persisted.Status is QueueStatus.Skipped or QueueStatus.Waiting && persisted.Visit!.Status == VisitStatuses.Created && persisted.Visit.StartedAt == null), "Race keeps Queue and Visit consistent");
        }
    }
    private sealed class Rendezvous : SaveChangesInterceptor
    {
        private int arrivals; private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        { if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); return result; }
    }
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added)) throw new InvalidOperationException("Synthetic absence audit failure");
            return ValueTask.FromResult(result);
        }
    }
}
