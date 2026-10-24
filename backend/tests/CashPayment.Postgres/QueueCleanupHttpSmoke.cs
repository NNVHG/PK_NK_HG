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

internal static class QueueCleanupHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        Action<string> authenticate, Action<string, int> authenticateActor, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Owned test database required");
        if (!await db.Roles.AnyAsync(r => r.RoleCode == RoleCodes.Patient))
        {
            db.Roles.Add(new Role { RoleCode = RoleCodes.Patient, RoleName = "Synthetic patient" });
            await db.SaveChangesAsync();
        }
        var today = new VietnamClock(SmokeTestTimeProvider.Instance).Today;
        async Task<QueueEntry> Entry(DateOnly date, QueueStatus status = QueueStatus.Waiting, string clinical = VisitStatuses.Created, bool locked = false)
        {
            var patient = new Patient { FullName = "Synthetic calling patient", DateOfBirth = new(1990, 1, 1), Phone = "0999999943" };
            var entry = new QueueEntry { Patient = patient, QueueDate = date,
                QueueNumber = (await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0) + 1, CheckInTime = DateTime.UtcNow,
                Status = status, Visit = new Visit { Patient = patient, CreatedByUserId = actorId, Status = clinical, IsLocked = locked, StartedAt = clinical == VisitStatuses.InProgress ? DateTime.UtcNow.AddDays(-1) : null } };
            db.QueueEntries.Add(entry); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return entry;
        }
        const string endpoint = "/api/queue/cleanup-expired";
        var payload = new { confirmed = true };
        var old = new List<QueueEntry>();
        foreach (var state in new[] { QueueStatus.Waiting, QueueStatus.Calling, QueueStatus.Skipped }) old.Add(await Entry(today.AddDays(-1), state));
        var started = await Entry(today.AddDays(-2), QueueStatus.Calling, VisitStatuses.InProgress);
        var locked = await Entry(today.AddDays(-1), locked: true);
        var current = await Entry(today); var future = await Entry(today.AddDays(1));
        var appointmentCount = await db.Appointments.CountAsync();
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.PostAsJsonAsync(endpoint, payload)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Cleanup anonymous401");
        foreach (var role in new[] { RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Patient })
        { authenticate(role); using var r = await client.PostAsJsonAsync(endpoint, payload); check(r.StatusCode == HttpStatusCode.Forbidden, $"Cleanup {role}403"); }
        authenticate(RoleCodes.Receptionist);
        using (var r = await client.PostAsJsonAsync(endpoint, new { confirmed = false })) check(r.StatusCode == HttpStatusCode.BadRequest, "Cleanup confirmation required400");
        using (var r = await client.PostAsJsonAsync(endpoint, payload))
        { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("cancelledCount").GetInt32() == 3 && json.EnumerateObject().Count() == 2, "Receptionist closes exactly three expired unstarted entries, minimal response"); }
        foreach (var entry in old)
        {
            var persisted = await db.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == entry.QueueEntryId);
            check(persisted.Status == QueueStatus.Cancelled && persisted.Visit!.Status == VisitStatuses.Cancelled && persisted.Visit.EndedAt != null, "Queue and Visit cancelled together");
            check(await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == entry.QueueEntryId && h.FromStatus == entry.Status && h.ToStatus == QueueStatus.Cancelled && h.ChangedByUserId == actorId), "Cleanup retains actual previous status and human actor");
            check(await db.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId == entry.QueueEntryId) == 1, "Cleanup requires one committed audit");
        }
        check(await db.Appointments.CountAsync() == appointmentCount, "Cleanup never synthesizes an appointment DL260");
        check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == started.QueueEntryId && q.Status == QueueStatus.Calling && q.Visit!.Status == VisitStatuses.InProgress), "Old active clinical Visit is untouched");
        check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == locked.QueueEntryId && q.Status == QueueStatus.Waiting && q.Visit!.IsLocked), "Locked Visit is untouched");
        check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == current.QueueEntryId && q.Status == QueueStatus.Waiting) &&
            await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == future.QueueEntryId && q.Status == QueueStatus.Waiting), "Before2330 current and future day remain open");
        authenticate(RoleCodes.Admin);
        using (var r = await client.PostAsJsonAsync(endpoint, payload)) { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("cancelledCount").GetInt32() == 0, "Admin repeat cleanup is idempotent zero"); }
        QueueService Service(DentalDbContext context)
        {
            var visits = new VisitRepository(context); var patients = new PatientRepository(context);
            var audit = new AuditLogger(context, NullLogger<AuditLogger>.Instance); var clock = new VietnamClock(SmokeTestTimeProvider.Instance);
            return new(new QueueRepository(context), new AppointmentRepository(context), patients, visits, new InvoiceRepository(context), new UserRepository(context),
                new UnitOfWork(context), audit, clock, new InvoiceDraftService(new InvoiceRepository(context), visits, patients, audit, clock), TestChairFixtures.Service(context, clock));
        }
        QueueCleanupService Cleanup(DentalDbContext context, VietnamClock? clock = null)
            => new(new QueueRepository(context), new UnitOfWork(context), new AuditLogger(context, NullLogger<AuditLogger>.Instance), clock ?? new VietnamClock(SmokeTestTimeProvider.Instance));
        var automatic = await Entry(today.AddDays(-3), QueueStatus.Skipped);
        await using (var context = new DentalDbContext(options)) check((await Cleanup(context).RunAutomaticallyAsync()).Value!.CancelledCount == 1, "Worker service catches up multiple missed days");
        check(await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == automatic.QueueEntryId && h.ChangedByUserId == null) &&
            await db.AuditLogs.AnyAsync(a => a.EntityType == "QueueEntry" && a.EntityId == automatic.QueueEntryId && a.UserId == null), "Automatic expiry uses system actor, never a fake user");
        using (var r = await client.GetAsync($"/api/queue/{automatic.QueueEntryId}")) { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); var history = json.GetProperty("history")[0]; check(r.Headers.CacheControl?.NoStore == true && history.GetProperty("changedByUserId").ValueKind == JsonValueKind.Null && history.GetProperty("changedByUserName").GetString() == "Hệ thống", "System history is readable and no-store"); }
        var returning = await Entry(today.AddDays(-1), QueueStatus.Skipped);
        authenticate(RoleCodes.Receptionist);
        using (var r = await client.PostAsJsonAsync("/api/queue/check-in", new { patientId = returning.PatientId, citizenId = "012345678901", phone = "0999999943" })) check(r.StatusCode == HttpStatusCode.Created, $"Next-day check-in closes old Skipped Visit then succeeds201 ({r.StatusCode}: {await r.Content.ReadAsStringAsync()})");
        check(await db.Visits.CountAsync(v => v.PatientId == returning.PatientId && v.Status == VisitStatuses.Created) == 1 &&
            await db.QueueEntries.CountAsync(q => q.PatientId == returning.PatientId && q.Status == QueueStatus.Waiting && q.QueueDate == today) == 1, "Returning patient has exactly one new open Visit and today's ticket");
        check(await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == returning.QueueEntryId && h.FromStatus == QueueStatus.Skipped && h.ToStatus == QueueStatus.Cancelled), "Check-in fallback records original Skipped status");
        var invalid = await Entry(today.AddDays(-1));
        using (var r = await client.PostAsJsonAsync("/api/queue/check-in", new { patientId = invalid.PatientId, appointmentId = int.MaxValue, citizenId = "012345678902", phone = "0999999943" })) check(r.StatusCode == HttpStatusCode.NotFound, "Invalid new appointment rejects check-in404");
        check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == invalid.QueueEntryId && q.Status == QueueStatus.Waiting) && !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == invalid.QueueEntryId), "Failed validation never partially cancels old ticket");
        using (var r = await client.PostAsJsonAsync("/api/queue/check-in", new { patientId = started.PatientId, citizenId = "012345678903", phone = "0999999943" })) check(r.StatusCode == HttpStatusCode.Conflict, "Old started clinical Visit blocks new check-in409 without auto-cancel");
        var fault = await Entry(today.AddDays(-1), QueueStatus.Calling);
        var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;
        foreach (var viaCheckIn in new[] { false, true })
        {
            await using var context = new DentalDbContext(faultOptions); var threw = false;
            try { if (viaCheckIn) await Service(context).CheckInAsync(actorId, new(fault.PatientId), null); else await Cleanup(context).RunAutomaticallyAsync(); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic cleanup audit failure") { threw = true; }
            check(threw, "Required cleanup audit failure propagates");
            check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == fault.QueueEntryId && q.Status == QueueStatus.Calling && q.Visit!.Status == VisitStatuses.Created) &&
                !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == fault.QueueEntryId) && await db.Visits.CountAsync(v => v.PatientId == fault.PatientId) == 1, "Failed cleanup/check-in rolls back old status, history, new Visit");
        }
        // The existing check-in audit itself must also be atomic with its new Visit/ticket.
        var fresh = new Patient { FullName = "Synthetic fresh patient", Phone = "0999999949" }; db.Patients.Add(fresh); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await using (var context = new DentalDbContext(faultOptions))
        {
            var threw = false; try { await Service(context).CheckInAsync(actorId, new(fresh.PatientId), null); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic cleanup audit failure") { threw = true; }
            check(threw && !await db.Visits.AnyAsync(v => v.PatientId == fresh.PatientId) && !await db.QueueEntries.AnyAsync(q => q.PatientId == fresh.PatientId), "Check-in audit failure leaves no orphan Visit or ticket");
        }
        await using (var context = new DentalDbContext(options)) await Cleanup(context).RunAutomaticallyAsync(); // Isolate races from older fixtures.
        var race = await Entry(today.AddDays(-1), QueueStatus.Skipped);
        var raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        async Task<bool> Expire()
        { await using var context = new DentalDbContext(raceOptions); var result = await Cleanup(context).RunAutomaticallyAsync(); check(result.IsSuccess || result.Error.Code == "CHK_010", "Cleanup race returns success/conflict only"); return result.IsSuccess; }
        async Task<bool> CheckIn()
        { await using var context = new DentalDbContext(raceOptions); var result = await Service(context).CheckInAsync(actorId, new(race.PatientId), null); check(result.IsSuccess || result.Error.Code == "CHK_010", "Check-in race returns success/conflict only"); return result.IsSuccess; }
        var outcomes = await Task.WhenAll(Expire(), CheckIn()); check(outcomes.Count(x => x) == 1, "Cleanup versus next-day check-in has one committed transaction");
        check(await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == race.QueueEntryId && h.ToStatus == QueueStatus.Cancelled) == 1, "Race never cancels old ticket twice");
        await using (var context = new DentalDbContext(options))
        {
            if (!await context.QueueEntries.AnyAsync(q => q.PatientId == race.PatientId && q.QueueDate == today)) check((await Service(context).CheckInAsync(actorId, new(race.PatientId), null)).IsSuccess, "Retry after cleanup winner can issue today's ticket");
        }
        check(await db.Visits.CountAsync(v => v.PatientId == race.PatientId && v.Status == VisitStatuses.Created) == 1, "Race/retry leaves exactly one open Visit");
        // Allocate the same next number from two snapshots; loser must roll back its new Visit as well.
        var numberPatients = new[] { new Patient { FullName = "Synthetic number A", Phone = "0999999947" }, new Patient { FullName = "Synthetic number B", Phone = "0999999948" } };
        db.Patients.AddRange(numberPatients); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var numberOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new QueueNumberRendezvous()).Options;
        async Task<Result<QueueEntryResponse>> NumberCheckIn(int patientId)
        { await using var context = new DentalDbContext(numberOptions); return await Service(context).CheckInAsync(actorId, new(patientId), null); }
        var numberResults = await Task.WhenAll(NumberCheckIn(numberPatients[0].PatientId), NumberCheckIn(numberPatients[1].PatientId));
        check(numberResults.Count(r => r.IsSuccess) == 1 && numberResults.Count(r => r.Error.Code == "CHK_010") == 1, "Concurrent number allocation returns one commit and one reload conflict");
        var numberIds = numberPatients.Select(p => p.PatientId).ToArray();
        check(await db.Visits.CountAsync(v => numberIds.Contains(v.PatientId)) == 1 && await db.QueueEntries.CountAsync(q => numberIds.Contains(q.PatientId)) == 1, "Number conflict rolls back orphan Visit and queue/history writes");
        var losingPatient = numberPatients[Array.FindIndex(numberResults, r => r.IsFailure)].PatientId;
        await using (var context = new DentalDbContext(options)) check((await Service(context).CheckInAsync(actorId, new(losingPatient), null)).IsSuccess, "Explicit retry after number conflict allocates next unique ticket");
        check(await db.Visits.CountAsync(v => numberIds.Contains(v.PatientId) && v.Status == VisitStatuses.Created) == 2, "Number retry leaves one open Visit per patient");
        // Boundary on real PostgreSQL, including same-day expiry and no early close.
        var boundary = await Entry(today, QueueStatus.Calling);
        var localLate = new DateTimeOffset(today.Year, today.Month, today.Day, 16, 29, 0, TimeSpan.Zero);
        await using (var context = new DentalDbContext(options)) check((await Cleanup(context, new(new FixedTime(localLate))).RunAutomaticallyAsync()).Value!.CancelledCount == 0, "23:29 Vietnam never closes current day");
        await using (var context = new DentalDbContext(options)) check((await Cleanup(context, new(new FixedTime(localLate.AddMinutes(1)))).RunAutomaticallyAsync()).Value!.CancelledCount >= 1, "23:30 Vietnam closes current unstarted day");
        check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == boundary.QueueEntryId && q.Status == QueueStatus.Cancelled) &&
            await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == started.QueueEntryId && q.Status == QueueStatus.Calling), "Boundary closes only unstarted and retains started Visit");
        check(await db.Appointments.CountAsync() == appointmentCount, "No branch ever invents an appointment or deletes history");
    }
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class Rendezvous : SaveChangesInterceptor
    {
        private int arrivals; private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        { if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); return result; }
    }
    private sealed class QueueNumberRendezvous : SaveChangesInterceptor
    {
        private int arrivals; private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (!data.Context!.ChangeTracker.Entries<QueueEntry>().Any(e => e.State == EntityState.Added)) return result;
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); return result;
        }
    }
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added)) throw new InvalidOperationException("Synthetic cleanup audit failure");
            return ValueTask.FromResult(result);
        }
    }
}
