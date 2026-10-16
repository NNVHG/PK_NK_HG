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

internal static class DeskReschedulingHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate, Action<bool, string> check)
    {
        await using var setup = new DentalDbContext(options);
        var clock = new VietnamClock(SmokeTestTimeProvider.Instance); var oldDay = clock.Today.AddDays(20); var newDay = oldDay.AddDays(1);
        Appointment Make(string name, int? dentist = null)
        {
            var row = new Appointment { Patient = new Patient { FullName = name, DateOfBirth = new(1990, 1, 1), Phone = "0999999910" },
                AppointmentDate = oldDay, SlotTime = new(8, 0), CreatedByUserId = actorId, Notes = "Original note", Status = AppointmentStatuses.Scheduled, DentistId = dentist };
            setup.Appointments.Add(row); return row;
        }
        var moved = Make("Synthetic moved"); var failedAudit = Make("Synthetic audit failure");
        var role = await setup.Roles.SingleOrDefaultAsync(r => r.RoleCode == RoleCodes.Dentist);
        if (role is null)
        {
            role = new Role { RoleCode = RoleCodes.Dentist, RoleName = "Synthetic reschedule dentist" };
            setup.Roles.Add(role); await setup.SaveChangesAsync();
        }
        var dentist = new User { RoleId = role.RoleId, FullName = "Synthetic scheduling dentist", Phone = "0999999911", IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
        setup.Users.Add(dentist); await setup.SaveChangesAsync();
        var busy = Make("Synthetic busy dentist", dentist.UserId); busy.AppointmentDate = newDay; busy.SlotTime = new(9, 0);
        var conflict = Make("Synthetic conflicting", dentist.UserId);
        var racer1 = Make("Synthetic racer one", dentist.UserId); racer1.SlotTime = new(10, 0);
        var racer2 = Make("Synthetic racer two", dentist.UserId); racer2.SlotTime = new(10, 30);
        var checkedIn = Make("Synthetic checked-in"); checkedIn.Status = AppointmentStatuses.CheckedIn;
        await setup.SaveChangesAsync(); setup.ChangeTracker.Clear();
        var originalCreatedAt = await setup.Appointments.AsNoTracking().Where(a => a.AppointmentId == moved.AppointmentId).Select(a => a.CreatedAt).SingleAsync();
        var request = new RescheduleAppointmentRequest(newDay, new(9, 0), Reason: "Synthetic shift");
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{moved.AppointmentId}/reschedule", request)) check(response.StatusCode == HttpStatusCode.Unauthorized, "Desk reschedule anonymous 401");
        foreach (var roleCode in new[] { RoleCodes.Dentist, RoleCodes.Assistant })
        { authenticate(roleCode); using var response = await client.PutAsJsonAsync($"/api/appointments/{moved.AppointmentId}/reschedule", request); check(response.StatusCode == HttpStatusCode.Forbidden, $"Desk reschedule {roleCode} 403"); }
        authenticate(RoleCodes.Patient);
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{moved.AppointmentId}/reschedule", request)) check(response.StatusCode == HttpStatusCode.NotFound, "Patient reschedule foreign desk appointment 404");
        authenticate(RoleCodes.Receptionist);
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{moved.AppointmentId}/reschedule", request))
        {
            var data = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
            check(response.StatusCode == HttpStatusCode.OK && data?.AppointmentId == moved.AppointmentId && data.RescheduleCount == 1 && data.RescheduleReason == request.Reason, "Desk reschedule in-place, count and reason returned");
        }
        await using (var verify = new DentalDbContext(options))
        {
            var row = await verify.Appointments.AsNoTracking().SingleAsync(a => a.AppointmentId == moved.AppointmentId);
            check(row.AppointmentDate == newDay && row.SlotTime == request.SlotTime && row.Notes == "Original note" && row.CreatedAt == originalCreatedAt, "Desk reschedule persists target and preserves original notes/identity");
            check(await new AppointmentRepository(verify).GetSlotBookingCountAsync(oldDay, moved.SlotTime) >= 0 && !await verify.Appointments.AnyAsync(a => a.AppointmentId == moved.AppointmentId && a.AppointmentDate == oldDay), "Desk reschedule releases old slot");
            var audit = await verify.AuditLogs.SingleAsync(a => a.EntityType == "Appointment" && a.EntityId == moved.AppointmentId && a.Action == AuditActions.AppointmentRescheduled);
            using var json = JsonDocument.Parse(audit.Detail!);
            check(json.RootElement.GetProperty("oldDate").GetString() == oldDay.ToString("yyyy-MM-dd") && json.RootElement.GetProperty("newDate").GetString() == newDay.ToString("yyyy-MM-dd") && !audit.Detail!.Contains(request.Reason!), "Desk reschedule audit old/new times and no free-text reason");
        }
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{moved.AppointmentId}/reschedule", request))
            check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadFromJsonAsync<AppointmentResponse>())?.RescheduleCount == 1, "Desk reschedule same-target replay is no-op");
        authenticate(RoleCodes.Admin);
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{moved.AppointmentId}/reschedule", request with { AppointmentDate = newDay.AddDays(1), Reason = new string('x', 500) }))
            check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadFromJsonAsync<AppointmentResponse>())?.RescheduleCount == 2, "Desk reschedule Admin and boundary500 reason");
        foreach (var invalid in new[] { request with { Reason = null }, request with { Reason = new string('x', 501) }, request with { SlotTime = new(12, 0) }, request with { AppointmentDate = clock.Today.AddDays(-1) } })
        { using var response = await client.PutAsJsonAsync($"/api/appointments/{moved.AppointmentId}/reschedule", invalid); check(response.StatusCode == HttpStatusCode.BadRequest, "Desk reschedule invalid reason/time/date 400"); }
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{conflict.AppointmentId}/reschedule", request)) check(response.StatusCode == HttpStatusCode.BadRequest, "Desk reschedule dentist slot conflict 400");
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{checkedIn.AppointmentId}/reschedule", request)) check(response.StatusCode == HttpStatusCode.BadRequest, "Desk reschedule checked-in cannot move");
        using (var response = await client.PutAsJsonAsync("/api/appointments/2147483647/reschedule", request)) check(response.StatusCode == HttpStatusCode.NotFound, "Desk reschedule missing appointment 404");
        var barrier = new Rendezvous();
        var racingOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(barrier).Options;
        AppointmentService Service(DentalDbContext db, IAuditLogger audit) => new(new AppointmentRepository(db), new PatientRepository(db), new UserRepository(db), new UnitOfWork(db), audit, clock);
        async Task<Result<AppointmentResponse>> Race(int id)
        {
            await using var db = new DentalDbContext(racingOptions);
            return await Service(db, new AuditLogger(db, NullLogger<AuditLogger>.Instance)).RescheduleAppointmentAsync(id, actorId, RoleCodes.Receptionist,
                request with { AppointmentDate = newDay.AddDays(3) }, null);
        }
        var races = await Task.WhenAll(Race(racer1.AppointmentId), Race(racer2.AppointmentId));
        check(races.Count(r => r.IsSuccess) == 1 && races.Count(r => r.Error.Code == "APP_009") == 1, "Desk reschedule deterministic serializable race returns one success/one conflict");
        await using (var verify = new DentalDbContext(options))
            check(await verify.Appointments.CountAsync(a => a.DentistId == dentist.UserId && a.AppointmentDate == newDay.AddDays(3) && a.SlotTime == request.SlotTime) == 1, "Desk reschedule race never double-books dentist");
        await using (var failing = new DentalDbContext(options))
        {
            var caught = false;
            try { await Service(failing, new ThrowAudit()).RescheduleAppointmentAsync(failedAudit.AppointmentId, actorId, RoleCodes.Admin, request, null); }
            catch (InvalidOperationException) { caught = true; }
            check(caught, "Desk reschedule unexpected audit failure propagates");
        }
        await using (var verify = new DentalDbContext(options))
        {
            var row = await verify.Appointments.AsNoTracking().SingleAsync(a => a.AppointmentId == failedAudit.AppointmentId);
            check(row.AppointmentDate == oldDay && row.RescheduleCount == 0 && row.RescheduleReason == null, "Desk reschedule transaction rolls back metadata and date on thrown audit failure");
            check(await verify.AuditLogs.CountAsync(a => a.EntityType == "Appointment" && a.EntityId == moved.AppointmentId && a.Action == AuditActions.AppointmentRescheduled) == 2, "Desk reschedule replay/rejections add no audit");
        }
    }
    private sealed class Rendezvous : SaveChangesInterceptor
    {
        private int arrivals;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }
    private sealed class ThrowAudit : IAuditLogger
    {
        public Task LogAsync(string action, string entityType, int? entityId = null, int? userId = null, string? detail = null, string? ipAddress = null, CancellationToken ct = default)
            => throw new InvalidOperationException("Synthetic audit failure");
    }
}
