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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

internal static class AutomaticReminderHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate,
        AppointmentReminderWorkerStatus status, Action<bool, string> check)
    {
        const string url = "/api/appointments/reminder-worker-status";
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync(url)) check(response.StatusCode == HttpStatusCode.Unauthorized, "Automatic reminder status anonymous401");
        foreach (var denied in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        { authenticate(denied); using var response = await client.GetAsync(url); check(response.StatusCode == HttpStatusCode.Forbidden, $"Automatic reminder status {denied}403"); }
        foreach (var allowed in new[] { RoleCodes.Admin, RoleCodes.Receptionist })
        {
            authenticate(allowed); using var response = await client.GetAsync(url);
            var result = await response.Content.ReadFromJsonAsync<ReminderWorkerStatusResponse>();
            check(response.StatusCode == HttpStatusCode.OK && result?.State == "NotStarted" && result.IntervalMinutes == 30 && result.CreatedCount == null, $"Automatic reminder status {allowed} initial state honest");
            check(response.Headers.CacheControl?.NoStore == true, "Automatic reminder status no-store");
        }
        await using var setup = new DentalDbContext(options);
        var day = new VietnamClock(TimeProvider.System).Today.AddDays(80);
        var time = new SteppedTime(new DateTimeOffset(day.Year, day.Month, day.Day, 2, 0, 0, TimeSpan.Zero));
        Appointment Make(string name, int hour) => new() { Patient = new Patient { FullName = name, Phone = "0999999971", DateOfBirth = new(1990, 1, 1) },
            AppointmentDate = day.AddDays(1), SlotTime = new(hour, 0), Status = AppointmentStatuses.Scheduled, CreatedByUserId = actorId, CreatedAt = time.GetUtcNow().UtcDateTime.AddDays(-2) };
        var immediate = Make("Synthetic automatic first", 9); var next = Make("Synthetic automatic later", 10);
        setup.Appointments.AddRange(immediate, next); await setup.SaveChangesAsync(); setup.ChangeTracker.Clear();
        ServiceProvider Services(DbContextOptions<DentalDbContext> dbOptions)
        {
            var services = new ServiceCollection();
            services.AddSingleton<TimeProvider>(time); services.AddSingleton(new VietnamClock(time)); services.AddSingleton(status);
            services.AddScoped(_ => new DentalDbContext(dbOptions)); services.AddScoped<IAppointmentReminderRepository, AppointmentReminderRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>(); services.AddScoped<IAuditLogger>(sp => new AuditLogger(sp.GetRequiredService<DentalDbContext>(), NullLogger<AuditLogger>.Instance));
            services.AddScoped<AppointmentReminderService>(); return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }
        async Task WaitCompleted(DateTime utc)
        {
            for (var attempt = 0; attempt < 500; attempt++)
            { var current = status.GetStatus(); if (current.LastCompletedAtUtc >= utc && current.State is "Success" or "Conflict" or "Failed") return; await Task.Delay(10); }
            throw new InvalidOperationException("Synthetic automatic scan timeout");
        }
        await using (var services = Services(options))
        using (var worker = new AppointmentReminderWorker(services.GetRequiredService<IServiceScopeFactory>(), time, status, NullLogger<AppointmentReminderWorker>.Instance))
        {
            var start = time.GetUtcNow().UtcDateTime; await worker.StartAsync(CancellationToken.None); await WaitCompleted(start);
            check(status.GetStatus() is { State: "Success", CreatedCount: 1 }, "Automatic worker first scan commits eligible occurrence");
            check(await setup.NotificationLogs.CountAsync(l => l.AppointmentId == immediate.AppointmentId) == 1 && !await setup.NotificationLogs.AnyAsync(l => l.AppointmentId == next.AppointmentId), "Automatic worker obeys current24h window");
            var log = await setup.NotificationLogs.AsNoTracking().SingleAsync(l => l.AppointmentId == immediate.AppointmentId);
            var audit = await setup.AuditLogs.AsNoTracking().Where(a => a.Action == "MOD_APP_REMINDERS_SIMULATED").OrderByDescending(a => a.LogId).FirstAsync();
            using (var detail = JsonDocument.Parse(audit.Detail!))
                check(audit.UserId == null && audit.IpAddress == null && detail.RootElement.GetProperty("source").GetString() == "Automatic" && !audit.Detail!.Contains(immediate.Patient.Phone), "Automatic worker audit uses system actor/source and no PII");
            check(log.SentAtUtc == start && log.Channel == "SMS_SIMULATED", "Automatic worker stores provider UTC time and simulation channel");
            using (var response = await client.GetAsync(url))
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                check(json.RootElement.GetProperty("state").GetString() == "Success" && json.RootElement.GetProperty("createdCount").GetInt32() == 1 && !json.RootElement.TryGetProperty("messageContent", out _), "Automatic status API exposes successful counts but no messages");
            }
            await using (var scope = services.CreateAsyncScope())
            {
                var manual = await scope.ServiceProvider.GetRequiredService<AppointmentReminderService>().SimulateAsync(actorId, RoleCodes.Receptionist, true, null);
                check(manual.IsSuccess && manual.Value!.CreatedCount == 0 && manual.Value.AlreadyRemindedCount == 1, "Manual replay after automatic scan shares occurrence dedupe");
            }
            time.Tick(); await WaitCompleted(time.GetUtcNow().UtcDateTime);
            check(time.Period == TimeSpan.FromMinutes(30) && status.GetStatus() is { State: "Success", CreatedCount: 1, AlreadyRemindedCount: 1 }, "Automatic30minute tick scans next window without duplicating old log");
            check(await setup.NotificationLogs.CountAsync(l => l.AppointmentId == next.AppointmentId) == 1 && await setup.NotificationLogs.CountAsync(l => l.AppointmentId == immediate.AppointmentId) == 1, "Automatic next tick commits exactly one new occurrence");
            await worker.StopAsync(CancellationToken.None); check(status.GetStatus().State == "Stopped" && time.Disposed, "Automatic worker stops and disposes timer");
        }
        // Race a fresh automatic startup with a staff scan, using the same fake clock and real database.
        var raceRow = Make("Synthetic automatic manual race", 10); setup.Appointments.Add(raceRow); await setup.SaveChangesAsync(); setup.ChangeTracker.Clear();
        var raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        await using (var services = Services(raceOptions))
        using (var worker = new AppointmentReminderWorker(services.GetRequiredService<IServiceScopeFactory>(), time, status, NullLogger<AppointmentReminderWorker>.Instance))
        {
            var start = time.GetUtcNow().UtcDateTime; await worker.StartAsync(CancellationToken.None);
            await using var scope = services.CreateAsyncScope();
            var manual = await scope.ServiceProvider.GetRequiredService<AppointmentReminderService>().SimulateAsync(actorId, RoleCodes.Receptionist, true, null);
            await WaitCompleted(start);
            check((status.GetStatus().State == "Success") != manual.IsSuccess && (manual.IsSuccess || manual.Error.Code == "APP_010"), "Automatic versus manual race has exactly one committed scan");
            check(await setup.NotificationLogs.CountAsync(l => l.AppointmentId == raceRow.AppointmentId) == 1, "Automatic versus manual race persists one log");
            await worker.StopAsync(CancellationToken.None);
        }
        using (var response = await client.GetAsync(url)) check((await response.Content.ReadFromJsonAsync<ReminderWorkerStatusResponse>())?.State == "Stopped", "Automatic status reflects controlled worker stop");
        check(new AppointmentReminderWorkerStatus().GetStatus() is { State: "NotStarted", LastCompletedAtUtc: null }, "Automatic status resets on process restart while stored logs remain");
    }
    private sealed class Rendezvous : SaveChangesInterceptor
    {
        private int arrivals; private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken); return result; }
    }
    private sealed class SteppedTime(DateTimeOffset now) : TimeProvider
    {
        private TestTimer? timer; public TimeSpan Period; public bool Disposed => timer?.Disposed == true;
        public override DateTimeOffset GetUtcNow() => now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { Period = period; timer = new(callback, state); return timer; }
        public void Tick() { now += TimeSpan.FromMinutes(30); timer!.Fire(); }
        private sealed class TestTimer(TimerCallback callback, object? state) : ITimer
        {
            public bool Disposed; public void Fire() { if (!Disposed) callback(state); }
            public bool Change(TimeSpan dueTime, TimeSpan period) => !Disposed;
            public void Dispose() => Disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
