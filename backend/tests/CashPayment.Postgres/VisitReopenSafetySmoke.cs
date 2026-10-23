using System.Net;
using System.Net.Http.Json;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
internal static class VisitReopenSafetySmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actor, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Reopen fixtures require owned isolated database.");
        var role = await db.Roles.SingleAsync(r => r.RoleCode == RoleCodes.Dentist);
        async Task<User> Dentist() {
            var user = new User { RoleId = role.RoleId, FullName = "Synthetic reopen dentist", Phone = "07" + Random.Shared.Next(10000000, 99999999),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), IsActive = true };
            db.Users.Add(user); await db.SaveChangesAsync(); return user;
        }
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(45);
        var number = (await db.QueueEntries.Where(q => q.QueueDate == date).Select(q => (int?)q.QueueNumber).MaxAsync()) ?? 0;
        async Task<(Visit Visit, Invoice Invoice, QueueEntry Queue)> Fixture(int dentist) {
            var patient = new Patient { FullName = "Synthetic reopen patient", Phone = "03" + Random.Shared.Next(10000000, 99999999), DateOfBirth = new(2000, 1, 1) };
            db.Patients.Add(patient); await db.SaveChangesAsync();
            var visit = new Visit { PatientId = patient.PatientId, DentistId = dentist, CreatedByUserId = actor,
                Status = VisitStatuses.Completed, IsLocked = true, EndedAt = DateTime.UtcNow };
            var invoice = new Invoice { Visit = visit, InvoiceCode = "REOPEN-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                Status = InvoiceStatus.PendingPayment, TotalAmount = 100000, CreatedByUserId = actor, CreatedAt = DateTime.UtcNow,
                Items = [new InvoiceItem { Code = "SNAP", Name = "Synthetic stored service", Quantity = 2, UnitPrice = 50000, TotalAmount = 100000 }] };
            var queue = new QueueEntry { PatientId = patient.PatientId, DentistId = dentist, Visit = visit, QueueDate = date,
                QueueNumber = ++number, Status = QueueStatus.Completed, CheckInTime = DateTime.UtcNow };
            db.Invoices.Add(invoice); db.QueueEntries.Add(queue); await db.SaveChangesAsync(); return (visit, invoice, queue);
        }
        async Task<bool> Untouched((Visit Visit, Invoice Invoice, QueueEntry Queue) f) =>
            await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == f.Visit.VisitId && v.Status == VisitStatuses.Completed && v.IsLocked) &&
            await db.Invoices.AsNoTracking().AnyAsync(i => i.Id == f.Invoice.Id && i.Status == InvoiceStatus.PendingPayment && i.PaidAmount == 0) &&
            await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == f.Queue.QueueEntryId && q.Status == QueueStatus.Completed) &&
            !await db.VisitUnlockRecords.AnyAsync(h => h.VisitId == f.Visit.VisitId) &&
            !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == f.Queue.QueueEntryId) &&
            !await db.AuditLogs.AnyAsync(a => a.EntityType == "Invoice" && a.EntityId == f.Invoice.Id);
        var dentist = await Dentist(); var f = await Fixture(dentist.UserId);
        var other = await Fixture(dentist.UserId);
        await db.Visits.Where(v => v.VisitId == other.Visit.VisitId).ExecuteUpdateAsync(s =>
            s.SetProperty(v => v.Status, VisitStatuses.InProgress).SetProperty(v => v.IsLocked, false));
        string Url(int id) => "/api/visits/" + id + "/unlock";
        const string reason = "Synthetic correction reason";
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.PostAsJsonAsync(Url(f.Visit.VisitId), new UnlockVisitRequest(reason))) check(r.StatusCode == HttpStatusCode.Unauthorized, "Reopen safety anonymous401");
        foreach (var denied in new[] { RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Patient }) {
            authenticate(denied); using var r = await client.PostAsJsonAsync(Url(f.Visit.VisitId), new UnlockVisitRequest(reason));
            check(r.StatusCode == HttpStatusCode.Forbidden, "Reopen safety Admin-only " + denied);
        }
        authenticate(RoleCodes.Admin);
        using (var r = await client.PostAsJsonAsync(Url(f.Visit.VisitId), new UnlockVisitRequest(reason))) check(r.StatusCode == HttpStatusCode.NoContent, "Correction is allowed while the dentist consults another patient");
        check(await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == f.Visit.VisitId && v.Status == VisitStatuses.Correcting && !v.IsLocked && v.DentistId == dentist.UserId),
            "Correction enters its own mode and keeps the assigned dentist");
        check(await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == other.Visit.VisitId && v.Status == VisitStatuses.InProgress && !v.IsLocked),
            "Correction leaves the other physical consultation unchanged");
        check(await db.Invoices.AsNoTracking().AnyAsync(i => i.Id == f.Invoice.Id && i.Status == InvoiceStatus.Cancelled && i.TotalAmount == 100000 && i.PaidAmount == 0) &&
            await db.InvoiceItems.AnyAsync(i => i.InvoiceId == f.Invoice.Id && i.Quantity == 2 && i.UnitPrice == 50000 && i.TotalAmount == 100000), "Reopen preserves cancelled invoice and snapshot lines");
        check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == f.Queue.QueueEntryId && q.Status == QueueStatus.Completed) &&
            await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == f.Queue.QueueEntryId) == 0, "Correction keeps the completed queue unchanged");
        var record = await db.VisitUnlockRecords.SingleAsync(h => h.VisitId == f.Visit.VisitId);
        check(record.Reason == reason && record.ActorUserId == actor && record.CancelledInvoiceId == f.Invoice.Id, "Reopen protected history contains confirmation reason and actor");
        var audits = await db.AuditLogs.Where(a => (a.EntityType == "Visit" && a.EntityId == f.Visit.VisitId) || (a.EntityType == "Invoice" && a.EntityId == f.Invoice.Id)).ToListAsync();
        check(audits.Count == 2 && audits.All(a => !a.Detail!.Contains(reason)), "Reopen audit refers to protected history without reason contents");
        using (var r = await client.PostAsJsonAsync(Url(f.Visit.VisitId), new UnlockVisitRequest(reason))) check(r.StatusCode == HttpStatusCode.Conflict, "Reopen same Visit cannot be reopened twice");
        check(await db.VisitUnlockRecords.CountAsync(h => h.VisitId == f.Visit.VisitId) == 1, "Reopen duplicate preserves single confirmation");
        var raceDentist = await Dentist(); var race = await Fixture(raceDentist.UserId);
        var raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        async Task<Dental.Application.Common.Result<VisitUnlockRecord>> Reopen(DbContextOptions<DentalDbContext> settings, int id) {
            await using var scoped = new DentalDbContext(settings); return await new VisitReopenRepository(scoped).ReopenAsync(id, actor, reason, null);
        }
        var outcomes = await Task.WhenAll(Reopen(raceOptions, race.Visit.VisitId), Reopen(raceOptions, race.Visit.VisitId));
        check(outcomes.Count(r => r.IsSuccess) == 1 && outcomes.Count(r => r.Error.Code == "BIL_006") == 1, "Concurrent correction of the same Visit commits once and rejects the competing write");
        check(await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == race.Visit.VisitId && v.Status == VisitStatuses.Correcting && !v.IsLocked), "Concurrent correction keeps the Visit in correction mode");
        check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == race.Queue.QueueEntryId && q.Status == QueueStatus.Completed) &&
            await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == race.Queue.QueueEntryId) == 0, "Concurrent correction does not reopen the physical queue");
        check(await db.VisitUnlockRecords.CountAsync(h => h.VisitId == race.Visit.VisitId) == 1, "Concurrent correction commits one protected confirmation history");
        var rollbackDentist = await Dentist(); var rollback = await Fixture(rollbackDentist.UserId);
        var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAfterWrite()).Options;
        var failed = false;
        try { await Reopen(faultOptions, rollback.Visit.VisitId); }
        catch (InvalidOperationException e) when (e.Message == "Synthetic reopen failure") { failed = true; }
        check(failed && await Untouched(rollback), "Correction failure after first Save rolls back Visit Invoice Queue history and audit");
        check((await Reopen(options, rollback.Visit.VisitId)).IsSuccess, "A fresh explicit correction request succeeds after rollback");
        authenticate(RoleCodes.Admin);
    }
    private sealed class Rendezvous : SaveChangesInterceptor {
        private int arrivals; private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default) {
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); return result;
        }
    }
    private sealed class FailAfterWrite : SaveChangesInterceptor {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
            => throw new InvalidOperationException("Synthetic reopen failure");
    }
}
