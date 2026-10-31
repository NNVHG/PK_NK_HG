using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_MST.Services;
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

internal static class PhysicalChairHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        Action<string, int> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Owned test database required");
        var clock = new VietnamClock(TimeProvider.System);
        async Task<User> Staff(string code, bool locate = true)
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == code);
            if (role is null) { role = new() { RoleCode = code, RoleName = code }; db.Roles.Add(role); await db.SaveChangesAsync(); }
            var user = new User { RoleId = role.RoleId, FullName = "Synthetic physical staff", IsActive = true,
                Phone = "PC" + Guid.NewGuid().ToString("N")[..14], PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
            db.Users.Add(user); await db.SaveChangesAsync();
            if (code == RoleCodes.Dentist && locate) await TestChairFixtures.EnsureAsync(db, user.UserId, actorId);
            db.ChangeTracker.Clear(); return user;
        }
        async Task<QueueEntry> Waiting(User dentist)
        {
            var patient = new Patient { FullName = "Synthetic physical patient", Phone = "0999999907", DateOfBirth = new(1990, 1, 1) };
            var entry = new QueueEntry { Patient = patient, DentistId = dentist.UserId, QueueDate = clock.Today,
                QueueNumber = (await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0) + 1,
                CheckInTime = DateTime.UtcNow, Status = QueueStatus.Waiting,
                Visit = new Visit { Patient = patient, Status = VisitStatuses.Created, CreatedByUserId = actorId } };
            db.QueueEntries.Add(entry); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return entry;
        }
        async Task<DentalChair> Chair(User dentist)
        {
            var id = await db.StaffWorkspaces.AsNoTracking().Where(w => w.UserId == dentist.UserId).Select(w => w.ChairId).SingleAsync();
            return await db.DentalChairs.AsNoTracking().SingleAsync(c => c.Id == id);
        }
        async Task<HttpStatusCode> Status(QueueEntry row, int status, string? reason = null)
        { using var r = await client.PutAsJsonAsync($"/api/queue/{row.QueueEntryId}/status", new { newStatus = status, reason }); return r.StatusCode; }
        var doctor = await Staff(RoleCodes.Dentist, false); var admin = await Staff(RoleCodes.Admin); var assistant = await Staff(RoleCodes.Assistant);
        var row = await Waiting(doctor); authenticate(RoleCodes.Dentist, doctor.UserId);
        using (var r = await client.PostAsJsonAsync("/api/queue/call-next", new { })) check(r.StatusCode == HttpStatusCode.Conflict, "Calling without workspace fails409 before trapping a pending call");
        check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == row.QueueEntryId && q.Status == QueueStatus.Waiting) &&
            !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == row.QueueEntryId), "Failed readiness leaves queue/history untouched");
        check(await Status(row, 2) == HttpStatusCode.Conflict, "Direct clinical start also requires ready chair409");
        await TestChairFixtures.EnsureAsync(db, doctor.UserId, actorId); db.ChangeTracker.Clear();
        var originalChair = await Chair(doctor);
        using (var r = await client.PostAsJsonAsync("/api/queue/call-next", new { })) check(r.StatusCode == HttpStatusCode.OK, "Ready dentist may invite without starting200");
        check((await Chair(doctor)).Status == DentalChairStatus.Available &&
            await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == row.VisitId && v.ChairId == null), "Calling never occupies or assigns physical chair");
        check(await Status(row, 2) == HttpStatusCode.OK, "Explicit clinical start succeeds200");
        var occupied = await Chair(doctor);
        check(occupied.Status == DentalChairStatus.Occupied && occupied.Version == originalChair.Version + 1 && occupied.UpdatedByUserId == doctor.UserId,
            "Start atomically sets Occupied/version/actor");
        check(await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == row.VisitId && v.Status == VisitStatuses.InProgress && v.ChairId == occupied.Id && v.RoomId == occupied.RoomId), "Visit retains actual allocated ChairId and RoomId");
        using (var r = await client.PutAsJsonAsync($"/api/workspaces/{doctor.UserId}", new { chairId = (int?)null, dentistId = (int?)null, expectedVersion = 1, confirmed = true }))
            check(r.StatusCode == HttpStatusCode.Conflict, "Active consultation blocks moving workspace409");
        check(await Status(row, 5, "Synthetic imaging indication") == HttpStatusCode.OK, "Dentist may route active chair visit to imaging200");
        check((await Chair(doctor)).Version == occupied.Version && (await Chair(doctor)).Status == DentalChairStatus.Occupied, "Imaging retains occupied chair while Visit stays InProgress");
        authenticate(RoleCodes.Assistant, assistant.UserId); check(await Status(row, 1) == HttpStatusCode.OK, "Assistant returns imaging to waiting200");
        using (var r = await client.PostAsJsonAsync("/api/queue/call-next", new { dentistId = doctor.UserId })) check(r.StatusCode == HttpStatusCode.OK, "Return invitation accepts the same Visit's occupied chair");
        authenticate(RoleCodes.Dentist, doctor.UserId); check(await Status(row, 2) == HttpStatusCode.OK, "Imaging return resumes clinical queue200");
        check((await Chair(doctor)).Version == occupied.Version, "Resume does not claim chair twice");
        using (var r = await client.PutAsync($"/api/visits/{row.VisitId}/complete", null)) check(r.StatusCode == HttpStatusCode.OK, "Physical completion and invoice succeed in one transaction200");
        check((await Chair(doctor)).Status == DentalChairStatus.Available && (await Chair(doctor)).Version == occupied.Version + 1, "Completion releases chair to Available");
        check(await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == row.VisitId && v.Status == VisitStatuses.Completed && v.IsLocked && v.ChairId == occupied.Id) &&
            await db.Invoices.AnyAsync(i => i.VisitId == row.VisitId && i.Status == InvoiceStatus.PendingPayment), "Completed Visit retains allocation history with pending invoice");
        var next = await Waiting(doctor); check(await Status(next, 2) == HttpStatusCode.OK, "Next patient can use released chair200");
        var nextChair = await Chair(doctor);
        authenticate(RoleCodes.Admin, admin.UserId);
        using (var r = await client.PostAsJsonAsync($"/api/visits/{row.VisitId}/unlock", new { unlockReason = "Synthetic physical history correction" }))
            check(r.StatusCode == HttpStatusCode.NoContent, "Correcting old visit with historical same chair does not block active patient204");
        using (var r = await client.PutAsync($"/api/visits/{row.VisitId}/complete", null)) check(r.StatusCode == HttpStatusCode.NoContent, "Correction completes independently of occupied chair204");
        check((await Chair(doctor)).Status == DentalChairStatus.Occupied && (await Chair(doctor)).Version == nextChair.Version &&
            await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == next.VisitId && v.Status == VisitStatuses.InProgress), "Correction cannot release or move next patient's occupied chair");
        check(await Status(next, 3) == HttpStatusCode.OK, "Admin may complete physical consultation200");
        var cancelledDoctor = await Staff(RoleCodes.Dentist); var cancelled = await Waiting(cancelledDoctor);
        authenticate(RoleCodes.Dentist, cancelledDoctor.UserId); check(await Status(cancelled, 2) == HttpStatusCode.OK, "Cancellation fixture starts physical visit");
        check(await Status(cancelled, 5, "Synthetic cancellation imaging") == HttpStatusCode.OK, "Cancellation fixture enters imaging");
        foreach (var role in new[] { RoleCodes.Assistant, RoleCodes.Receptionist })
        { authenticate(role, assistant.UserId); check(await Status(cancelled, 4, "Synthetic forbidden cancellation") == HttpStatusCode.Forbidden, "Clinical imaging cancellation excludes Assistant/Receptionist403 per DL057"); }
        authenticate(RoleCodes.Dentist, doctor.UserId); check(await Status(cancelled, 4, "Synthetic foreign cancellation") == HttpStatusCode.Forbidden, "Other dentist cannot cancel allocated Visit403");
        authenticate(RoleCodes.Dentist, cancelledDoctor.UserId); check(await Status(cancelled, 4) == HttpStatusCode.BadRequest, "Clinical cancellation requires reason400");
        check(await Status(cancelled, 4, "Synthetic clinical cancellation") == HttpStatusCode.OK, "Assigned dentist cancels imaging visit200");
        check((await Chair(cancelledDoctor)).Status == DentalChairStatus.Available &&
            await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == cancelled.VisitId && v.Status == VisitStatuses.Cancelled && v.ChairId != null), "Cancellation releases chair while preserving clinical allocation history");

        QueueService Service(DentalDbContext context)
        {
            var visits = new VisitRepository(context); var patients = new PatientRepository(context); var audit = new AuditLogger(context, NullLogger<AuditLogger>.Instance);
            return new(new QueueRepository(context), new AppointmentRepository(context), patients, visits, new InvoiceRepository(context), new UserRepository(context), new UnitOfWork(context), audit, clock,
                new InvoiceDraftService(new InvoiceRepository(context), visits, patients, audit, clock), TestChairFixtures.Service(context, clock));
        }
        var failedDoctor = await Staff(RoleCodes.Dentist); var failed = await Waiting(failedDoctor);
        var failOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailQueueAudit()).Options;
        async Task AuditFailure(int status)
        {
            await using var context = new DentalDbContext(failOptions); var failedAudit = false;
            try { await Service(context).UpdateQueueStatusAsync(failed.QueueEntryId, failedDoctor.UserId, RoleCodes.Dentist, new(status, Reason: "Synthetic rollback reason"), null); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic chair audit failure") { failedAudit = true; }
            check(failedAudit, "Required queue audit failure propagates for physical transition");
        }
        await AuditFailure(2);
        check((await Chair(failedDoctor)).Status == DentalChairStatus.Available && (await Chair(failedDoctor)).Version == 1 &&
            await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == failed.VisitId && v.Status == VisitStatuses.Created && v.ChairId == null) &&
            !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == failed.QueueEntryId), "Failed start rolls back chair/version/Visit/history");
        authenticate(RoleCodes.Dentist, failedDoctor.UserId); check(await Status(failed, 2) == HttpStatusCode.OK, "Start succeeds after rollback");
        await AuditFailure(3);
        check((await Chair(failedDoctor)).Status == DentalChairStatus.Occupied && (await Chair(failedDoctor)).Version == 2 &&
            await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == failed.VisitId && v.Status == VisitStatuses.InProgress && !v.IsLocked) &&
            !await db.Invoices.AnyAsync(i => i.VisitId == failed.VisitId) && await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == failed.QueueEntryId) == 1,
            "Failed completion rolls back release/Visit/queue/invoice/history across enlisted transaction");
        await AuditFailure(4);
        check((await Chair(failedDoctor)).Status == DentalChairStatus.Occupied &&
            await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == failed.QueueEntryId && q.Status == QueueStatus.InConsultation), "Failed cancellation retains occupied active consultation");

        var raceDoctor = await Staff(RoleCodes.Dentist); var left = await Waiting(raceDoctor); var right = await Waiting(raceDoctor);
        var barrier = new SaveBarrier(); var raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(barrier).Options;
        async Task<bool> Start(QueueEntry target)
        {
            await using var context = new DentalDbContext(raceOptions);
            return (await Service(context).UpdateQueueStatusAsync(target.QueueEntryId, raceDoctor.UserId, RoleCodes.Dentist, new(2), null)).IsSuccess;
        }
        check((await Task.WhenAll(Start(left), Start(right))).Count(success => success) == 1, "Concurrent physical starts have one winner");
        var raceChair = await Chair(raceDoctor);
        check(raceChair.Status == DentalChairStatus.Occupied && await db.Visits.CountAsync(v => v.ChairId == raceChair.Id && v.Status == VisitStatuses.InProgress) == 1,
            "Chair allocation concurrency preserves one active Visit per chair");
        check(await db.Visits.CountAsync(v => (v.VisitId == left.VisitId || v.VisitId == right.VisitId) && v.Status == VisitStatuses.Created && v.ChairId == null) == 1 &&
            await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == left.QueueEntryId || h.QueueEntryId == right.QueueEntryId) == 1, "Losing physical start leaves no partial allocation/history");
        // Verify the database guard, independent of service preconditions.
        var duplicate = new Visit { PatientId = left.PatientId, DentistId = doctor.UserId, CreatedByUserId = actorId, Status = VisitStatuses.InProgress, ChairId = raceChair.Id };
        db.Visits.Add(duplicate); var rejected = false;
        try { await db.SaveChangesAsync(); } catch (DbUpdateException) { rejected = true; }
        db.ChangeTracker.Clear(); check(rejected, "PostgreSQL unique partial index independently rejects another active Visit on occupied chair");
        if (raceChair.RoomId.HasValue)
        {
            var duplicateRoom = new Visit { PatientId = left.PatientId, DentistId = doctor.UserId, CreatedByUserId = actorId, Status = VisitStatuses.InProgress, RoomId = raceChair.RoomId.Value };
            db.Visits.Add(duplicateRoom); var roomRejected = false;
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { roomRejected = true; }
            db.ChangeTracker.Clear(); check(roomRejected, "PostgreSQL unique partial index independently rejects another active Visit on occupied room");
        }

        var movingDoctor = await Staff(RoleCodes.Dentist); var movingVisit = await Waiting(movingDoctor); var priorChair = await Chair(movingDoctor);
        var destination = new DentalChair { ChairNumber = "MOVE-" + Guid.NewGuid().ToString("N"), RoomName = "Synthetic destination room",
            Status = DentalChairStatus.Available, IsActive = true, CreatedByUserId = actorId };
        db.DentalChairs.Add(destination); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var moveBarrier = new SaveBarrier(); var moveOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(moveBarrier).Options;
        async Task<bool> StartWhileMoving()
        {
            await using var context = new DentalDbContext(moveOptions);
            return (await Service(context).UpdateQueueStatusAsync(movingVisit.QueueEntryId, movingDoctor.UserId, RoleCodes.Dentist, new(2), null)).IsSuccess;
        }
        async Task<bool> MoveWhileStarting()
        {
            await using var context = new DentalDbContext(moveOptions);
            var workspace = new StaffWorkspaceService(new StaffWorkspaceRepository(context), new DentalChairRepository(context), new UserRepository(context),
                new VisitRepository(context), new QueueRepository(context), new UnitOfWork(context), new AuditLogger(context, NullLogger<AuditLogger>.Instance), clock);
            return (await workspace.ChangeAsync(movingDoctor.UserId, RoleCodes.Dentist, movingDoctor.UserId, new(destination.Id, null, 1, true))).IsSuccess;
        }
        check((await Task.WhenAll(StartWhileMoving(), MoveWhileStarting())).Count(success => success) == 1, "Workspace move versus clinical start has one winner");
        var afterRace = await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == movingVisit.VisitId);
        var currentChair = await Chair(movingDoctor);
        check(afterRace.Status == VisitStatuses.InProgress && afterRace.ChairId == currentChair.Id && currentChair.Status == DentalChairStatus.Occupied ||
            afterRace.Status == VisitStatuses.Created && afterRace.ChairId == null && currentChair.Id == destination.Id && currentChair.Status == DentalChairStatus.Available,
            "Move/start race never detaches active Visit from its dentist workspace");
        await QueueTransferHttpSmoke.RunAsync(client, options, actorId, authenticate, check);
    }
    private sealed class FailQueueAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.Action == AuditActions.QueueStatusChanged))
                throw new InvalidOperationException("Synthetic chair audit failure");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class SaveBarrier : SaveChangesInterceptor
    {
        private readonly ConcurrentDictionary<Guid, byte> seen = new();
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (seen.TryAdd(data.Context!.ContextId.InstanceId, 0))
            { if (seen.Count == 2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); }
            return result;
        }
    }
}
