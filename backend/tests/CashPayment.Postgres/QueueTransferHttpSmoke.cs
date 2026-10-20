using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
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

internal static class QueueTransferHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        Action<string, int> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Owned test database required");
        var clock = new VietnamClock(TimeProvider.System);
        async Task<User> Staff(string code, bool locate = true)
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == code);
            if (role is null) { role = new() { RoleCode = code, RoleName = code }; db.Roles.Add(role); await db.SaveChangesAsync(); }
            var user = new User { RoleId = role.RoleId, FullName = "Synthetic transfer staff", IsActive = true,
                Phone = "QT" + Guid.NewGuid().ToString("N")[..14], PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
            db.Users.Add(user); await db.SaveChangesAsync();
            if (code == RoleCodes.Dentist && locate) await TestChairFixtures.EnsureAsync(db, user.UserId, actorId);
            db.ChangeTracker.Clear(); return user;
        }
        async Task<QueueEntry> Row(User dentist, bool active = true, QueueStatus status = QueueStatus.InConsultation)
        {
            var patient = new Patient { FullName = "Synthetic transfer patient", Phone = "0999999911", DateOfBirth = new(1990, 1, 1) };
            var chair = active ? await Chair(dentist) : null;
            if (chair is not null) { chair.Status = DentalChairStatus.Occupied; db.DentalChairs.Update(chair); }
            var row = new QueueEntry { Patient = patient, DentistId = dentist.UserId, QueueDate = clock.Today,
                QueueNumber = (await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0) + 1,
                CheckInTime = clock.UtcNow, Status = active ? status : QueueStatus.Waiting,
                Visit = new Visit { Patient = patient, CreatedByUserId = actorId,
                    Status = active ? VisitStatuses.InProgress : VisitStatuses.Created,
                    DentistId = active ? dentist.UserId : null, ChairId = chair?.Id, StartedAt = active ? clock.UtcNow : null } };
            db.QueueEntries.Add(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return row;
        }
        async Task<DentalChair> Chair(User dentist)
        {
            var id = await db.StaffWorkspaces.AsNoTracking().Where(w => w.UserId == dentist.UserId).Select(w => w.ChairId).SingleAsync();
            return await db.DentalChairs.AsNoTracking().SingleAsync(c => c.Id == id);
        }
        async Task<HttpStatusCode> Transfer(QueueEntry row, User source, User target, int? expectedStatus = null)
        {
            using var response = await client.PostAsJsonAsync($"/api/queue/{row.QueueEntryId}/transfer",
                new TransferDentistRequest(target.UserId, true, "Synthetic transfer reason", source.UserId, expectedStatus ?? (int)row.Status));
            return response.StatusCode;
        }
        QueueService Service(DentalDbContext context)
        {
            var visits = new VisitRepository(context); var patients = new PatientRepository(context);
            var audit = new AuditLogger(context, NullLogger<AuditLogger>.Instance);
            return new(new QueueRepository(context), new AppointmentRepository(context), patients, visits,
                new InvoiceRepository(context), new UserRepository(context), new UnitOfWork(context), audit, clock,
                new InvoiceDraftService(new InvoiceRepository(context), visits, patients, audit, clock), TestChairFixtures.Service(context, clock));
        }

        var receptionist = await Staff(RoleCodes.Receptionist); var assistant = await Staff(RoleCodes.Assistant);
        var admin = await Staff(RoleCodes.Admin); var source = await Staff(RoleCodes.Dentist); var target = await Staff(RoleCodes.Dentist);
        var row = await Row(source); var oldChair = await Chair(source); var newChair = await Chair(target);
        foreach (var role in new[] { RoleCodes.Assistant, RoleCodes.Patient })
        {
            authenticate(role, assistant.UserId);
            check(await Transfer(row, source, target) == HttpStatusCode.Forbidden, "Transfer excludes Assistant/Patient403");
        }
        client.DefaultRequestHeaders.Authorization = null;
        check(await Transfer(row, source, target) == HttpStatusCode.Unauthorized, "Transfer requires JWT401");
        authenticate(RoleCodes.Dentist, target.UserId);
        check(await Transfer(row, source, target) == HttpStatusCode.Forbidden, "Foreign dentist cannot transfer403");
        authenticate(RoleCodes.Receptionist, receptionist.UserId);
        using (var directory = await client.GetAsync("/api/queue/call-dentists"))
            check(directory.StatusCode == HttpStatusCode.OK && directory.Headers.CacheControl?.NoStore == true,
                "Receptionist reads minimal dentist directory with NoStore");
        using (var invalid = await client.PostAsJsonAsync($"/api/queue/{row.QueueEntryId}/transfer",
            new TransferDentistRequest(target.UserId, false, ExpectedDentistId: source.UserId, ExpectedStatus: 2)))
            check(invalid.StatusCode == HttpStatusCode.BadRequest, "Transfer requires explicit confirmation400");
        using (var invalid = await client.PostAsJsonAsync($"/api/queue/{row.QueueEntryId}/transfer",
            new TransferDentistRequest(target.UserId, true, new string('x', 501), source.UserId, 2)))
            check(invalid.StatusCode == HttpStatusCode.BadRequest, "Transfer reason bounded500 characters400");

        var noWorkspace = await Staff(RoleCodes.Dentist, false);
        check(await Transfer(row, source, noWorkspace) == HttpStatusCode.Conflict, "Missing target workspace blocks transfer409");
        foreach (var status in new[] { DentalChairStatus.Occupied, DentalChairStatus.Maintenance, DentalChairStatus.Inactive })
        {
            var blockedChair = await Chair(target); blockedChair.Status = status;
            db.DentalChairs.Update(blockedChair); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            check(await Transfer(row, source, target) == HttpStatusCode.Conflict, "Unavailable target chair blocks transfer409");
            check((await Chair(source)).Status == DentalChairStatus.Occupied &&
                await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == row.VisitId && v.ChairId == oldChair.Id && v.DentistId == source.UserId) &&
                !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == row.QueueEntryId), "Rejected transfer never partially releases source or writes history");
        }
        newChair.Status = DentalChairStatus.Available; db.DentalChairs.Update(newChair); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        using (var response = await client.PostAsJsonAsync($"/api/queue/{row.QueueEntryId}/transfer",
            new TransferDentistRequest(target.UserId, true, "Synthetic transfer reason", source.UserId, 2)))
        {
            var result = await response.Content.ReadFromJsonAsync<QueueEntryResponse>();
            check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true &&
                result?.DentistId == target.UserId && result.DentistName == target.FullName && result.ChairNumber == newChair.ChairNumber,
                "Receptionist transfer returns current destination and NoStore200");
        }
        check((await Chair(source)).Status == DentalChairStatus.Available && (await Chair(source)).Version == oldChair.Version + 1 &&
            (await Chair(target)).Status == DentalChairStatus.Occupied && (await Chair(target)).Version == newChair.Version + 1,
            "Transfer updates both chair statuses and versions atomically");
        check(await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == row.VisitId && v.ChairId == newChair.Id && v.DentistId == target.UserId && v.Status == VisitStatuses.InProgress) &&
            await db.QueueEntries.AnyAsync(q => q.QueueEntryId == row.QueueEntryId && q.DentistId == target.UserId && q.Status == QueueStatus.InConsultation),
            "Transfer preserves Visit and queue status while updating both assignments");
        check(await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == row.QueueEntryId && h.FromStatus == h.ToStatus && h.ChangedByUserId == receptionist.UserId) == 1 &&
            await db.AuditLogs.AnyAsync(a => a.EntityId == row.QueueEntryId && a.Action == AuditActions.QueueStatusChanged && a.Detail!.Contains("TransferDentist")),
            "Transfer writes append-only history and required audit");
        check(await Transfer(row, source, target) == HttpStatusCode.Conflict, "Sequential stale confirmation cannot silently transfer again409");
        authenticate(RoleCodes.Dentist, source.UserId);
        using (var clinical = await client.PutAsJsonAsync($"/api/visits/{row.VisitId}/diagnosis", new { diagnosis = "Synthetic" }))
            check(clinical.StatusCode == HttpStatusCode.Forbidden, "Former dentist loses clinical write access after transfer403");

        var waitingSource = await Staff(RoleCodes.Dentist); var waitingTarget = await Staff(RoleCodes.Dentist, false);
        var waiting = await Row(waitingSource, false); authenticate(RoleCodes.Admin, admin.UserId);
        check(await Transfer(waiting, waitingSource, waitingTarget) == HttpStatusCode.OK, "Waiting transfer does not require or occupy destination chair200");
        check(await db.Visits.AnyAsync(v => v.VisitId == waiting.VisitId && v.DentistId == waitingTarget.UserId && v.ChairId == null && v.Status == VisitStatuses.Created),
            "Waiting transfer preserves unstarted Visit and assigns dentist");

        var failSource = await Staff(RoleCodes.Dentist); var failTarget = await Staff(RoleCodes.Dentist); var failed = await Row(failSource);
        var failOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailTransferAudit()).Options;
        await using (var context = new DentalDbContext(failOptions))
        {
            var rejected = false;
            try { await Service(context).TransferDentistAsync(failed.QueueEntryId, admin.UserId, RoleCodes.Admin,
                new(failTarget.UserId, true, "Synthetic rollback", failSource.UserId, 2), null); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic transfer audit failure") { rejected = true; }
            check(rejected, "Required audit failure propagates");
        }
        check((await Chair(failSource)).Status == DentalChairStatus.Occupied && (await Chair(failTarget)).Status == DentalChairStatus.Available &&
            await db.Visits.AnyAsync(v => v.VisitId == failed.VisitId && v.DentistId == failSource.UserId) &&
            !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == failed.QueueEntryId), "Audit failure rolls back Visit, both chairs, assignment and history");

        var raceSourceA = await Staff(RoleCodes.Dentist); var raceSourceB = await Staff(RoleCodes.Dentist); var raceTarget = await Staff(RoleCodes.Dentist);
        var left = await Row(raceSourceA); var right = await Row(raceSourceB);
        var raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new SaveBarrier()).Options;
        async Task<bool> Race(QueueEntry entry, User from)
        {
            await using var context = new DentalDbContext(raceOptions);
            return (await Service(context).TransferDentistAsync(entry.QueueEntryId, admin.UserId, RoleCodes.Admin,
                new(raceTarget.UserId, true, "Synthetic race", from.UserId, 2), null)).IsSuccess;
        }
        check((await Task.WhenAll(Race(left, raceSourceA), Race(right, raceSourceB))).Count(result => result) == 1,
            "Concurrent transfers to one destination have exactly one winner");
        var raceChair = await Chair(raceTarget);
        check(await db.Visits.CountAsync(v => v.ChairId == raceChair.Id && v.Status == VisitStatuses.InProgress) == 1 &&
            await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == left.QueueEntryId || h.QueueEntryId == right.QueueEntryId) == 1,
            "Losing transfer keeps original chair/assignment and writes no history");
    }

    private sealed class FailTransferAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.Detail?.Contains("TransferDentist") == true))
                throw new InvalidOperationException("Synthetic transfer audit failure");
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
