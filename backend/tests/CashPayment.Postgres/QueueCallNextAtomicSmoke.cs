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
        var day = new VietnamClock(SmokeTestTimeProvider.Instance).Today.AddDays(91);
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
        check(await setup.QueueEntries.AsNoTracking().CountAsync(q => q.QueueDate == day && q.Status == QueueStatus.Calling) == 1 &&
            await setup.Visits.AsNoTracking().CountAsync(v => v.DentistId == sameDentist.UserId && v.Status == VisitStatuses.InProgress) == 0,
            "Call-next same-dentist race persists one Calling and no started clinical Visit");
        check(await setup.QueueStatusHistories.CountAsync(h => h.QueueEntryId == first.QueueEntryId) == 1 &&
            await setup.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId == first.QueueEntryId && a.Action == AuditActions.QueueStatusChanged) == 1 &&
            !await setup.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == second.QueueEntryId), "Call-next race writes one history/audit only");
        var busy = await Call(options, day, sameDentist.UserId);
        check(busy.IsFailure && busy.Error.Code == Error.QueueDentistAlreadyCalling.Code, "Call-next sequential retry cannot invite a second patient");

        var left = await Dentist(); var right = await Dentist(); var shared = await Waiting(day.AddDays(1), 1);
        raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        var sharedRace = await Task.WhenAll(Call(raceOptions, shared.QueueDate, left.UserId), Call(raceOptions, shared.QueueDate, right.UserId));
        var winner = sharedRace.Single(r => r.IsSuccess).Value!;
        check(sharedRace.Count(r => r.IsSuccess) == 1 && sharedRace.Count(r => r.Error.Code == "CHK_010") == 1,
            "Call-next two-dentist race claims shared waiting entry once");
        var persisted = await setup.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == shared.QueueEntryId);
        check(persisted.DentistId == winner.DentistId && persisted.Visit!.DentistId == null && persisted.Visit.Status == VisitStatuses.Created && persisted.Visit.StartedAt == null &&
            await setup.QueueStatusHistories.CountAsync(h => h.QueueEntryId == shared.QueueEntryId) == 1,
            "Call-next shared claim reserves queue for winning dentist without starting Visit");

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

        async Task<Result<QueueEntryResponse>> Cancel(DbContextOptions<DentalDbContext> dbOptions, QueueEntry row)
        {
            await using var db = new DentalDbContext(dbOptions);
            return await Service(db, row.QueueDate).CancelWaitingAsync(row.QueueEntryId, actorId, RoleCodes.Receptionist, new(true, "Synthetic reason"), null);
        }
        var cancelRaceEntry = await Waiting(day.AddDays(4), 1);
        raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        var cancelRace = await Task.WhenAll(Cancel(raceOptions, cancelRaceEntry), Cancel(raceOptions, cancelRaceEntry));
        check(cancelRace.Count(r => r.IsSuccess) == 1 && cancelRace.Count(r => r.Error.Code == "CHK_012") == 1,
            "Cancel waiting concurrent cancellations one commit/one CHK_012");
        check(await setup.QueueStatusHistories.CountAsync(h => h.QueueEntryId == cancelRaceEntry.QueueEntryId) == 1 &&
            await setup.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId == cancelRaceEntry.QueueEntryId) == 1,
            "Cancel waiting race persists one history and audit");
        var racingDentist = await Dentist(); var claimOrCancel = await Waiting(day.AddDays(5), 1, racingDentist.UserId);
        raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        var mixedRace = await Task.WhenAll(Cancel(raceOptions, claimOrCancel), Call(raceOptions, claimOrCancel.QueueDate, racingDentist.UserId));
        check(mixedRace.Count(r => r.IsSuccess) == 1 && mixedRace.Any(r => r.Error.Code is "CHK_010" or "CHK_012"),
            "Cancel waiting versus call-next race has one committed outcome");
        persisted = await setup.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == claimOrCancel.QueueEntryId);
        check((persisted.Status == QueueStatus.Cancelled && persisted.Visit!.Status == VisitStatuses.Cancelled && persisted.Visit.StartedAt == null) ||
            (persisted.Status == QueueStatus.Calling && persisted.Visit!.Status == VisitStatuses.Created && persisted.Visit.DentistId == null && persisted.DentistId == racingDentist.UserId),
            "Cancel waiting versus call-next never splits queue and Visit status");
        check(await setup.QueueStatusHistories.CountAsync(h => h.QueueEntryId == claimOrCancel.QueueEntryId) == 1 &&
            await setup.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId == claimOrCancel.QueueEntryId) == 1,
            "Cancel waiting versus call-next persists winner history and audit once");
        var rollbackCancel = await Waiting(day.AddDays(6), 1); failed = false;
        faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAfterWrite()).Options;
        try { await Cancel(faultOptions, rollbackCancel); }
        catch (InvalidOperationException e) when (e.Message == "Synthetic call-next failure after write") { failed = true; }
        persisted = await setup.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == rollbackCancel.QueueEntryId);
        check(failed && persisted.Status == QueueStatus.Waiting && persisted.Visit!.Status == VisitStatuses.Created && persisted.Visit.EndedAt == null,
            "Cancel waiting failure after write rolls back queue and Visit");
        check(!await setup.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == rollbackCancel.QueueEntryId) &&
            !await setup.AuditLogs.AnyAsync(a => a.EntityType == "QueueEntry" && a.EntityId == rollbackCancel.QueueEntryId),
            "Cancel waiting rollback leaves no history/audit");
        check((await Cancel(options, rollbackCancel)).IsSuccess, "Cancel waiting fresh request after rollback succeeds");
        async Task<Result<QueueEntryResponse>> StartSelected(DbContextOptions<DentalDbContext> dbOptions, QueueEntry row, int dentistId)
        {
            await using var db = new DentalDbContext(dbOptions);
            return await Service(db, row.QueueDate).UpdateQueueStatusAsync(row.QueueEntryId, dentistId, RoleCodes.Dentist, new(2, dentistId), null);
        }
        var selectedDentist = await Dentist(); var selectA = await Waiting(day.AddDays(7), 1); var selectB = await Waiting(day.AddDays(7), 2);
        raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        var selectedRace = await Task.WhenAll(StartSelected(raceOptions, selectA, selectedDentist.UserId), StartSelected(raceOptions, selectB, selectedDentist.UserId));
        check(selectedRace.Count(r => r.IsSuccess) == 1 && selectedRace.Count(r => r.Error.Code == "CHK_010") == 1,
            "Selected start different entries same dentist one commit/one conflict");
        check(await setup.Visits.AsNoTracking().CountAsync(v => v.DentistId == selectedDentist.UserId && v.Status == VisitStatuses.InProgress) == 1,
            "Selected start race cannot create two active Visits for dentist");
        var selectedIds = new[] { selectA.QueueEntryId, selectB.QueueEntryId };
        check(await setup.QueueStatusHistories.CountAsync(h => selectedIds.Contains(h.QueueEntryId)) == 1 &&
            await setup.AuditLogs.CountAsync(a => a.EntityType == "QueueEntry" && a.EntityId.HasValue && selectedIds.Contains(a.EntityId.Value)) == 1,
            "Selected start different-entry race commits one history/audit");
        var cancelVsStartDentist = await Dentist(); var cancelVsStart = await Waiting(day.AddDays(8), 1);
        raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        var selectedCancelRace = await Task.WhenAll(StartSelected(raceOptions, cancelVsStart, cancelVsStartDentist.UserId), Cancel(raceOptions, cancelVsStart));
        check(selectedCancelRace.Count(r => r.IsSuccess) == 1 && selectedCancelRace.Any(r => r.Error.Code is "CHK_010" or "CHK_012"),
            "Selected start versus cancellation commits one outcome");
        persisted = await setup.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == cancelVsStart.QueueEntryId);
        check((persisted.Status == QueueStatus.Cancelled && persisted.Visit!.Status == VisitStatuses.Cancelled) ||
            (persisted.Status == QueueStatus.InConsultation && persisted.Visit!.Status == VisitStatuses.InProgress),
            "Selected start versus cancel keeps Queue and Visit consistent");
        var nextVsStartDentist = await Dentist(); var nextVsStart = await Waiting(day.AddDays(9), 1, nextVsStartDentist.UserId);
        raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        var selectedNextRace = await Task.WhenAll(StartSelected(raceOptions, nextVsStart, nextVsStartDentist.UserId), Call(raceOptions, nextVsStart.QueueDate, nextVsStartDentist.UserId));
        check(selectedNextRace.Count(r => r.IsSuccess) == 1 && selectedNextRace.Count(r => r.Error.Code == "CHK_010") == 1,
            "Selected start versus call-next claims same entry once");
        check(await setup.QueueStatusHistories.CountAsync(h => h.QueueEntryId == nextVsStart.QueueEntryId) == 1,
            "Selected start versus call-next persists one history");
        var failSelectedDentist = await Dentist(); var failSelected = await Waiting(day.AddDays(10), 1); failed = false;
        faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAfterWrite()).Options;
        try { await StartSelected(faultOptions, failSelected, failSelectedDentist.UserId); }
        catch (InvalidOperationException e) when (e.Message == "Synthetic call-next failure after write") { failed = true; }
        persisted = await setup.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == failSelected.QueueEntryId);
        check(failed && persisted.Status == QueueStatus.Waiting && persisted.Visit!.Status == VisitStatuses.Created && persisted.Visit.StartedAt == null,
            "Selected start failure after write rolls back Queue and Visit");
        check(!await setup.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == failSelected.QueueEntryId) &&
            !await setup.AuditLogs.AnyAsync(a => a.EntityType == "QueueEntry" && a.EntityId == failSelected.QueueEntryId),
            "Selected start rollback leaves no history/audit");

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
