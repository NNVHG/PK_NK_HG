using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Features.Queue.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

internal static class QueueCallNextAtomicSmoke
{
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, int actorId, Action<bool, string> check)
    {
        await using var setup = new DentalDbContext(options);
        if (!setup.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Call-next fixtures require the owned isolated database.");
        var role = await setup.Roles.SingleAsync(r => r.RoleCode == RoleCodes.Dentist);
        var day = new VietnamClock(TimeProvider.System).Today.AddDays(91);
        async Task<User> Dentist()
        {
            var user = new User { RoleId = role.RoleId, FullName = "Synthetic atomic dentist", IsActive = true,
                Phone = "ATOMIC" + Guid.NewGuid().ToString("N")[..14], PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
            setup.Users.Add(user); await setup.SaveChangesAsync(); return user;
        }
        async Task<QueueEntry> Waiting(DateOnly date, int number, int? dentistId = null)
        {
            var entry = new QueueEntry { Patient = new Patient { FullName = "Synthetic atomic queue", Phone = "0999999951", DateOfBirth = new(1990, 1, 1) },
                Visit = new Visit { CreatedByUserId = actorId, Status = VisitStatuses.Created }, QueueDate = date,
                QueueNumber = number, DentistId = dentistId, CheckInTime = DateTime.UtcNow.AddMinutes(number), Status = QueueStatus.Waiting };
            entry.Visit.Patient = entry.Patient;
            setup.QueueEntries.Add(entry); await setup.SaveChangesAsync(); return entry;
        }
        QueueService Service(DentalDbContext db, DateOnly date)
        {
            var clock = new VietnamClock(new FixedTime(new DateTimeOffset(date.Year, date.Month, date.Day, 2, 0, 0, TimeSpan.Zero)));
            var visits = new VisitRepository(db); var patients = new PatientRepository(db);
            var audit = new AuditLogger(db, NullLogger<AuditLogger>.Instance);
            return new(new QueueRepository(db), new AppointmentRepository(db), patients, visits, new UserRepository(db),
                new UnitOfWork(db), audit, clock, new InvoiceDraftService(new InvoiceRepository(db), visits, patients, audit, clock));
        }
        async Task<Result<QueueEntryResponse>> Call(DbContextOptions<DentalDbContext> dbOptions, DateOnly date, int dentistId)
        {
            await using var db = new DentalDbContext(dbOptions);
            return await Service(db, date).CallNextAsync(dentistId, RoleCodes.Dentist, new(null), null);
        }

        var sameDentist = await Dentist(); var first = await Waiting(day, 1); var second = await Waiting(day, 2);
        var raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        var sameRace = await Task.WhenAll(Call(raceOptions, day, sameDentist.UserId), Call(raceOptions, day, sameDentist.UserId));
        check(sameRace.Count(r => r.IsSuccess) == 1 && sameRace.Count(r => r.Error.Code == "CHK_010") == 1,
            "Call-next same-dentist race has one commit and one CHK_010");
        check(await setup.QueueEntries.AsNoTracking().CountAsync(q => q.QueueDate == day && q.Status == QueueStatus.InConsultation) == 1 &&
            await setup.Visits.AsNoTracking().CountAsync(v => v.DentistId == sameDentist.UserId && v.Status == VisitStatuses.InProgress) == 1,
            "Call-next same-dentist race persists one active queue and Visit");
        check(await setup.QueueStatusHistories.CountAsync(h => h.QueueEntryId == first.QueueEntryId) == 1 &&
            await setup.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId == first.QueueEntryId && a.Action == AuditActions.QueueStatusChanged) == 1 &&
            !await setup.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == second.QueueEntryId), "Call-next race writes one history/audit only");
        var busy = await Call(options, day, sameDentist.UserId);
        check(busy.IsFailure && busy.Error.Code == Error.DentistHasActiveVisit.Code, "Call-next sequential retry cannot start a second Visit");

        var left = await Dentist(); var right = await Dentist(); var shared = await Waiting(day.AddDays(1), 1);
        raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        var sharedRace = await Task.WhenAll(Call(raceOptions, shared.QueueDate, left.UserId), Call(raceOptions, shared.QueueDate, right.UserId));
        var winner = sharedRace.Single(r => r.IsSuccess).Value!;
        check(sharedRace.Count(r => r.IsSuccess) == 1 && sharedRace.Count(r => r.Error.Code == "CHK_010") == 1,
            "Call-next two-dentist race claims shared waiting entry once");
        var persisted = await setup.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == shared.QueueEntryId);
        check(persisted.DentistId == winner.DentistId && persisted.Visit!.DentistId == winner.DentistId &&
            await setup.QueueStatusHistories.CountAsync(h => h.QueueEntryId == shared.QueueEntryId) == 1,
            "Call-next shared claim preserves winning dentist across queue/Visit/history");

        var rollbackDentist = await Dentist(); var rollback = await Waiting(day.AddDays(2), 1);
        var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAfterWrite()).Options;
        var failed = false;
        try { await Call(faultOptions, rollback.QueueDate, rollbackDentist.UserId); }
        catch (InvalidOperationException e) when (e.Message == "Synthetic call-next failure after write") { failed = true; }
        check(failed, "Call-next failure injected after first database write");
        persisted = await setup.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == rollback.QueueEntryId);
        check(persisted.Status == QueueStatus.Waiting && persisted.DentistId == null && persisted.Visit!.Status == VisitStatuses.Created &&
            persisted.Visit.DentistId == null && persisted.Visit.StartedAt == null, "Call-next failure rolls back queue and Visit");
        check(!await setup.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == rollback.QueueEntryId) &&
            !await setup.AuditLogs.AnyAsync(a => a.EntityType == "QueueEntry" && a.EntityId == rollback.QueueEntryId), "Call-next failure leaves no history/audit");
        check((await Call(options, rollback.QueueDate, rollbackDentist.UserId)).IsSuccess, "Call-next fresh request succeeds after rollback");

        var returningDentist = await Dentist(); var returning = await Waiting(day.AddDays(3), 1, returningDentist.UserId);
        var started = new DateTime(DateTime.UtcNow.AddHours(-1).Ticks / 10 * 10, DateTimeKind.Utc);
        returning.Visit!.Status = VisitStatuses.InProgress; returning.Visit.DentistId = returningDentist.UserId; returning.Visit.StartedAt = started;
        await setup.SaveChangesAsync();
        var resumed = await Call(options, returning.QueueDate, returningDentist.UserId);
        var resumedVisit = await setup.Visits.AsNoTracking().SingleAsync(v => v.VisitId == returning.VisitId);
        check(resumed.IsSuccess && resumed.Value!.VisitId == returning.VisitId && resumedVisit.StartedAt == started,
            "Call-next imaging return resumes same active Visit without replacing StartedAt");
    }
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class Rendezvous : SaveChangesInterceptor
    {
        private int arrivals; private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken); return result; }
    }
    private sealed class FailAfterWrite : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Synthetic call-next failure after write");
    }
}
