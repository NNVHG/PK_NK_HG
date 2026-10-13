using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Appointments.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

internal static class AppointmentReminderHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var now = new VietnamClock(TimeProvider.System).Now;
        var target = now.AddDays(1); target = new DateTime(target.Year, target.Month, target.Day, target.Hour, target.Minute, 0);
        Appointment Make(string name, string state = AppointmentStatuses.Scheduled, bool urgent = false, double offset = 0) => new()
        {
            Patient = new Patient { FullName = name, Phone = "0999999961", DateOfBirth = new(1990, 1, 1) },
            AppointmentDate = DateOnly.FromDateTime(target.AddHours(offset)), SlotTime = TimeOnly.FromDateTime(target.AddHours(offset)),
            Status = state, CreatedByUserId = actorId, CreatedAt = DateTime.UtcNow.AddHours(urgent ? 1 : -48)
        };
        var eligible = Make("Synthetic reminder eligible"); var urgent = Make("Synthetic reminder urgent", urgent: true);
        var cancelled = Make("Synthetic reminder cancelled", AppointmentStatuses.Cancelled);
        var checkedIn = Make("Synthetic reminder checked-in", AppointmentStatuses.CheckedIn);
        var outside = Make("Synthetic reminder outside", offset: -2);
        db.Appointments.AddRange(eligible, urgent, cancelled, checkedIn, outside); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        const string simulate = "/api/appointments/simulate-reminder"; const string list = "/api/appointments/reminder-logs";
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.PostAsJsonAsync(simulate, new SimulateAppointmentRemindersRequest(true))) check(response.StatusCode == HttpStatusCode.Unauthorized, "Reminder anonymous POST401");
        using (var response = await client.GetAsync(list)) check(response.StatusCode == HttpStatusCode.Unauthorized, "Reminder anonymous GET401");
        foreach (var denied in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(denied);
            using var write = await client.PostAsJsonAsync(simulate, new SimulateAppointmentRemindersRequest(true)); check(write.StatusCode == HttpStatusCode.Forbidden, $"Reminder {denied} cannot simulate");
            using var read = await client.GetAsync(list); check(read.StatusCode == HttpStatusCode.Forbidden, $"Reminder {denied} cannot read message logs");
        }
        authenticate(RoleCodes.Receptionist);
        using (var response = await client.PostAsJsonAsync(simulate, new SimulateAppointmentRemindersRequest(false))) check(response.StatusCode == HttpStatusCode.BadRequest, "Reminder requires explicit confirmation400");
        foreach (var query in new[] { "page=0", "pageSize=101", "appointmentId=-1", "page=1000001" })
        { using var response = await client.GetAsync(list + "?" + query); check(response.StatusCode == HttpStatusCode.BadRequest, "Reminder query validation400"); }
        using (var response = await client.PostAsJsonAsync(simulate, new SimulateAppointmentRemindersRequest(true)))
            check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadFromJsonAsync<SimulateAppointmentRemindersResponse>())!.CreatedCount >= 1, "Reminder simulated scan200 creates eligible rows");
        var stored = await db.NotificationLogs.AsNoTracking().SingleAsync(l => l.AppointmentId == eligible.AppointmentId);
        check(stored.Channel == "SMS_SIMULATED" && stored.Status == "Sent" && stored.RecipientPhone == eligible.Patient.Phone && stored.MessageContent.Contains("Synthetic reminder eligible") && stored.MessageContent.Contains("Hotline hỗ trợ: 0901234567"), "Reminder stored real snapshot/template/channel/status/phone");
        check(stored.AppointmentDate == eligible.AppointmentDate && stored.SlotTime == eligible.SlotTime && stored.SentAtUtc.Kind == DateTimeKind.Utc, "Reminder occurrence snapshots date/time and UTC sent timestamp");
        var excluded = new[] { urgent.AppointmentId, cancelled.AppointmentId, checkedIn.AppointmentId, outside.AppointmentId };
        check(!await db.NotificationLogs.AnyAsync(l => excluded.Contains(l.AppointmentId)), "Reminder skips urgent/cancelled/checked-in/outside window");
        using (var response = await client.GetAsync(list + $"?appointmentId={eligible.AppointmentId}"))
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var row = json.RootElement.GetProperty("items")[0];
            check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true, "Reminder logs200/no-store");
            check(!row.TryGetProperty("recipientPhone", out _) && row.GetProperty("messageContent").GetString() == stored.MessageContent, "Reminder log view contains intended message but omits recipient phone");
        }
        var auditBefore = await db.AuditLogs.CountAsync(a => a.Action == "MOD_APP_REMINDERS_SIMULATED");
        var details = await db.AuditLogs.Where(a => a.Action == "MOD_APP_REMINDERS_SIMULATED").Select(a => a.Detail).ToListAsync();
        check(details.All(d => !d!.Contains(eligible.Patient.Phone) && !d.Contains(eligible.Patient.FullName)), "Reminder audit never contains recipient/name/message");
        authenticate(RoleCodes.Admin);
        using (var response = await client.PostAsJsonAsync(simulate, new SimulateAppointmentRemindersRequest(true)))
            check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadFromJsonAsync<SimulateAppointmentRemindersResponse>())!.CreatedCount == 0, "Reminder replay creates zero new rows");
        check(await db.AuditLogs.CountAsync(a => a.Action == "MOD_APP_REMINDERS_SIMULATED") == auditBefore, "Reminder replay does not duplicate audit");
        // Change the stored schedule in this owned fixture. No API bypass is used in production.
        var change = await db.Appointments.SingleAsync(a => a.AppointmentId == eligible.AppointmentId);
        change.SlotTime = TimeOnly.FromDateTime(target.AddMinutes(20)); change.RescheduleCount++; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        using (var response = await client.PostAsJsonAsync(simulate, new SimulateAppointmentRemindersRequest(true))) check(response.StatusCode == HttpStatusCode.OK, "Reminder rescan after schedule change200");
        var movedLogs = await db.NotificationLogs.AsNoTracking().Where(l => l.AppointmentId == eligible.AppointmentId).ToListAsync();
        check(movedLogs.Count == 2 && movedLogs.Select(l => l.SlotTime).Distinct().Count() == 2 && movedLogs.Any(l => l.SlotTime == stored.SlotTime && l.MessageContent == stored.MessageContent), "Reminder changed occurrence adds new log and preserves old snapshot");
        using (var response = await client.PostAsJsonAsync(simulate, new SimulateAppointmentRemindersRequest(true))) check((await response.Content.ReadFromJsonAsync<SimulateAppointmentRemindersResponse>())!.CreatedCount == 0, "Reminder moved occurrence replay still deduplicates");

        // Both scans read the same unlogged occurrence before either writes.
        var raceRow = Make("Synthetic reminder race"); db.Appointments.Add(raceRow); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        AppointmentReminderService Service(DentalDbContext context, IAuditLogger? logger = null) => new(new AppointmentReminderRepository(context), new UnitOfWork(context), logger ?? new AuditLogger(context, NullLogger<AuditLogger>.Instance), new VietnamClock(TimeProvider.System), TimeProvider.System);
        async Task<Result<SimulateAppointmentRemindersResponse>> Race()
        { await using var context = new DentalDbContext(raceOptions); return await Service(context).SimulateAsync(actorId, RoleCodes.Receptionist, true, null); }
        var races = await Task.WhenAll(Race(), Race());
        check(races.Count(r => r.IsSuccess) == 1 && races.Count(r => r.Error.Code == "APP_010") == 1, "Reminder deterministic concurrent scans one success/one409 conflict");
        check(await db.NotificationLogs.CountAsync(l => l.AppointmentId == raceRow.AppointmentId) == 1, "Reminder concurrent scan never duplicates occurrence");
        var rollbackRow = Make("Synthetic reminder rollback"); db.Appointments.Add(rollbackRow); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await using (var failing = new DentalDbContext(options))
        {
            var caught = false; try { await Service(failing, new ThrowAudit()).SimulateAsync(actorId, RoleCodes.Admin, true, null); } catch (InvalidOperationException) { caught = true; }
            check(caught && !await db.NotificationLogs.AnyAsync(l => l.AppointmentId == rollbackRow.AppointmentId), "Reminder thrown audit failure rolls back log batch");
        }
        await using (var duplicate = new DentalDbContext(options))
        {
            var caught = false;
            try { await new UnitOfWork(duplicate).ExecuteSerializableTransactionAsync(async ct => { duplicate.NotificationLogs.Add(Copy(stored)); await duplicate.SaveChangesAsync(ct); return true; }); }
            catch (ConcurrentOperationException) { caught = true; }
            check(caught && !duplicate.ChangeTracker.Entries().Any(), "Reminder exact unique key23505 maps to conflict and clears tracker");
        }
        await using (var missingFk = new DentalDbContext(options))
        {
            var log = Copy(stored); log.AppointmentId = int.MaxValue; missingFk.NotificationLogs.Add(log);
            var caught = false; try { await missingFk.SaveChangesAsync(); } catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23503" }) { caught = true; }
            check(caught, "Reminder foreign key rejects nonexistent appointment");
        }
        await using (var immutable = new DentalDbContext(options))
        {
            var log = await immutable.NotificationLogs.FirstAsync(); log.MessageContent = "Synthetic tampering";
            var caught = false; try { await immutable.SaveChangesAsync(); } catch (InvalidOperationException) { caught = true; }
            check(caught, "Reminder stored content cannot be modified");
        }
        using (var response = await client.PostAsJsonAsync(simulate, new SimulateAppointmentRemindersRequest(true))) check(response.StatusCode == HttpStatusCode.OK, "Reminder retry after rolled back batch succeeds");
        check(await db.NotificationLogs.CountAsync(l => l.AppointmentId == rollbackRow.AppointmentId) == 1, "Reminder retry commits exactly one rollback occurrence");
        // Add synthetic historical snapshots to prove count/filter/pagination ordering.
        for (var n = 1; n <= 21; n++) { var log = Copy(stored); log.AppointmentDate = stored.AppointmentDate.AddDays(-n); log.SentAtUtc = DateTime.UtcNow.AddDays(-n); db.NotificationLogs.Add(log); }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        using (var response = await client.GetAsync(list + $"?appointmentId={eligible.AppointmentId}&page=2"))
        {
            var rows = await response.Content.ReadFromJsonAsync<PagedResult<ReminderLogResponse>>();
            check(rows?.TotalCount == 23 && rows.Items.Count == 3 && rows.Items.All(l => l.AppointmentId == eligible.AppointmentId), "Reminder logs filter/count/page2 excludes unrelated messages");
            check(rows!.Items.Zip(rows.Items.Skip(1)).All(pair => pair.First.SentAtUtc >= pair.Second.SentAtUtc), "Reminder logs newest-first ordering");
        }
    }
    private static NotificationLog Copy(NotificationLog log) => new() { AppointmentId = log.AppointmentId, PatientId = log.PatientId, AppointmentDate = log.AppointmentDate, SlotTime = log.SlotTime, RecipientPhone = log.RecipientPhone, MessageContent = log.MessageContent, SentAtUtc = DateTime.UtcNow };
    private sealed class Rendezvous : SaveChangesInterceptor
    {
        private int arrivals; private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken); return result; }
    }
    private sealed class ThrowAudit : IAuditLogger
    {
        public Task LogAsync(string action, string entityType, int? entityId = null, int? userId = null, string? detail = null, string? ipAddress = null, CancellationToken ct = default) => throw new InvalidOperationException("Synthetic reminder audit failure");
    }
}
