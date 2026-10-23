using System.Net;
using System.Net.Http.Json;
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

internal static class PatientSelfReschedulingHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var owner = await db.Patients.FirstAsync(p => p.UserId == actorId && p.IsActive);
        var foreign = new Patient { FullName = "Synthetic reschedule foreign", DateOfBirth = new(1990, 1, 1), Phone = "0999999942" };
        db.Patients.Add(foreign); await db.SaveChangesAsync();
        var clock = new VietnamClock(SmokeTestTimeProvider.Instance); var oldDay = clock.Today.AddDays(80); var targetDay = oldDay.AddDays(1);
        Appointment Make(int patientId, string state = AppointmentStatuses.Scheduled, int count = 0) => new()
        { PatientId = patientId, AppointmentDate = oldDay, SlotTime = new(9, 0), Status = state, CreatedByUserId = actorId, Notes = "Original note", RescheduleCount = count };
        var row = Make(owner.PatientId); var other = Make(foreign.PatientId); var near = Make(owner.PatientId);
        var nearTime = clock.Now.AddMinutes(30); near.AppointmentDate = DateOnly.FromDateTime(nearTime); near.SlotTime = TimeOnly.FromDateTime(nearTime);
        var limit = Make(owner.PatientId, count: 2); var closed = Make(owner.PatientId, AppointmentStatuses.CheckedIn);
        var racer = Make(owner.PatientId, count: 1);
        db.Appointments.AddRange(row, other, near, limit, closed, racer); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var auditBefore = await db.AuditLogs.CountAsync();
        string Eligibility(int id) => $"/api/appointments/{id}/rescheduling-eligibility";
        string Move(int id) => $"/api/appointments/{id}/reschedule";
        var request = new RescheduleAppointmentRequest(targetDay, new(10, 0), Reason: "Synthetic reason");
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync(Eligibility(row.AppointmentId))) check(response.StatusCode == HttpStatusCode.Unauthorized, "Self reschedule eligibility anonymous 401");
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant })
        { authenticate(role); using var response = await client.GetAsync(Eligibility(row.AppointmentId)); check(response.StatusCode == HttpStatusCode.Forbidden, $"Self reschedule eligibility {role} 403"); }
        authenticate(RoleCodes.Patient);
        using (var response = await client.GetAsync(Eligibility(row.AppointmentId)))
        {
            var data = await response.Content.ReadFromJsonAsync<SelfReschedulingEligibilityResponse>();
            check(response.StatusCode == HttpStatusCode.OK && data?.CanReschedule == true && data.RemainingChanges == 2 && data.Appointment.PatientId == owner.PatientId, "Self reschedule own patient snapshot eligible");
            check(response.Headers.CacheControl?.NoStore == true, "Self reschedule eligibility no-store");
        }
        foreach (var id in new[] { other.AppointmentId, int.MaxValue })
        {
            using var read = await client.GetAsync(Eligibility(id)); check(read.StatusCode == HttpStatusCode.NotFound, "Self reschedule foreign/missing eligibility 404");
            using var write = await client.PutAsJsonAsync(Move(id), request); check(write.StatusCode == HttpStatusCode.NotFound, "Self reschedule foreign/missing write 404");
        }
        using (var response = await client.GetAsync(Eligibility(0))) check(response.StatusCode == HttpStatusCode.BadRequest, "Self reschedule positive ID validation 400");
        foreach (var invalid in new[] { near, limit, closed })
        {
            using var read = await client.GetAsync(Eligibility(invalid.AppointmentId));
            var data = await read.Content.ReadFromJsonAsync<SelfReschedulingEligibilityResponse>();
            check(data?.CanReschedule == false && data.Message?.Contains("Lễ tân") == true, "Self reschedule deadline/count/state guides reception");
            using var write = await client.PutAsJsonAsync(Move(invalid.AppointmentId), request); check(write.StatusCode == HttpStatusCode.BadRequest, "Self reschedule write rechecks deadline/count/state");
        }
        check(await db.AuditLogs.CountAsync() == auditBefore, "Self reschedule GET/rejections never audit");
        foreach (var invalid in new[] { request with { Reason = null }, request with { Reason = new string('x', 501) }, request with { SlotTime = new(12, 0) } })
        { using var response = await client.PutAsJsonAsync(Move(row.AppointmentId), invalid); check(response.StatusCode == HttpStatusCode.BadRequest, "Self reschedule invalid reason/time 400"); }
        foreach (var forbidden in new[] { request with { DentistId = 1 }, request with { Notes = "Spoofed note" } })
        { using var response = await client.PutAsJsonAsync(Move(row.AppointmentId), forbidden); check(response.StatusCode == HttpStatusCode.Forbidden, "Self reschedule cannot change dentist/staff notes"); }
        using (var response = await client.PutAsJsonAsync(Move(row.AppointmentId), request))
        {
            var data = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
            check(response.StatusCode == HttpStatusCode.OK && data?.AppointmentId == row.AppointmentId && data.RescheduleCount == 1 && data.Notes == "Original note", "Self reschedule persists in-place, count and original staff notes");
        }
        using (var response = await client.PutAsJsonAsync(Move(row.AppointmentId), request))
            check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadFromJsonAsync<AppointmentResponse>())?.RescheduleCount == 1, "Self reschedule replay no-op");
        var second = request with { AppointmentDate = targetDay.AddDays(1) };
        using (var response = await client.PutAsJsonAsync(Move(row.AppointmentId), second))
            check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadFromJsonAsync<AppointmentResponse>())?.RescheduleCount == 2, "Self reschedule second move allowed");
        using (var response = await client.PutAsJsonAsync(Move(row.AppointmentId), request with { AppointmentDate = targetDay.AddDays(2) }))
            check(response.StatusCode == HttpStatusCode.BadRequest, "Self reschedule third move blocked");
        using (var response = await client.GetAsync(Eligibility(row.AppointmentId)))
            check((await response.Content.ReadFromJsonAsync<SelfReschedulingEligibilityResponse>()) is { CanReschedule: false, RemainingChanges: 0 }, "Self reschedule fresh eligibility reflects count2");
        var stored = await db.Appointments.AsNoTracking().SingleAsync(a => a.AppointmentId == row.AppointmentId);
        check(stored.AppointmentDate == second.AppointmentDate && stored.RescheduleCount == 2 && stored.RescheduleReason == request.Reason && stored.PatientId == owner.PatientId, "Self reschedule committed time/reason/identity retained");
        var auditRows = await db.AuditLogs.Where(a => a.Action == AuditActions.AppointmentRescheduled && a.EntityId == row.AppointmentId).ToListAsync();
        check(auditRows.Count == 2 && auditRows.All(a => a.UserId == actorId && !a.Detail!.Contains(request.Reason!)), "Self reschedule two audits from JWT actor and no free text");

        // Synchronize two writers after reading count1, before saving: only one may consume change2.
        var racingOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        async Task<Result<AppointmentResponse>> Race(int offset)
        {
            await using var racingDb = new DentalDbContext(racingOptions);
            var service = new AppointmentService(new AppointmentRepository(racingDb), new PatientRepository(racingDb), new UserRepository(racingDb), new UnitOfWork(racingDb), new AuditLogger(racingDb, NullLogger<AuditLogger>.Instance), clock);
            return await service.RescheduleAppointmentAsync(racer.AppointmentId, actorId, RoleCodes.Patient, request with { AppointmentDate = targetDay.AddDays(offset) }, null);
        }
        var raceResults = await Task.WhenAll(Race(10), Race(11));
        check(raceResults.Count(r => r.IsSuccess) == 1 && raceResults.Count(r => r.Error.Code == "APP_009") == 1, "Self reschedule count1 race one success/one conflict");
        var racingRow = await db.Appointments.AsNoTracking().SingleAsync(a => a.AppointmentId == racer.AppointmentId);
        check(racingRow.RescheduleCount == 2 && await db.AuditLogs.CountAsync(a => a.Action == AuditActions.AppointmentRescheduled && a.EntityId == racer.AppointmentId) == 1, "Self reschedule race persists count2 and one audit");
        using (var response = await client.PutAsJsonAsync(Move(racer.AppointmentId), request with { AppointmentDate = targetDay.AddDays(12) }))
            check(response.StatusCode == HttpStatusCode.BadRequest, "Self reschedule new attempt after race still blocked at count2");
        authenticate(RoleCodes.Receptionist);
        using (var response = await client.PutAsJsonAsync(Move(row.AppointmentId), request with { AppointmentDate = targetDay.AddDays(3) }))
            check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadFromJsonAsync<AppointmentResponse>())?.RescheduleCount == 3, "Reception can assist after self reschedule limit");
    }
    private sealed class Rendezvous : SaveChangesInterceptor
    {
        private int arrivals;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken); return result;
        }
    }
}
