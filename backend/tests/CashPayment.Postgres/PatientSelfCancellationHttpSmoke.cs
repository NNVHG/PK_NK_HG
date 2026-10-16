using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class PatientSelfCancellationHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var owner = await db.Patients.FirstAsync(p => p.UserId == actorId && p.IsActive);
        var other = new Patient { FullName = "Synthetic self-cancellation other", DateOfBirth = new(1990, 1, 1), Phone = "0999999921" };
        db.Patients.Add(other); await db.SaveChangesAsync();
        var clock = new VietnamClock(SmokeTestTimeProvider.Instance);
        Appointment Make(int id, string state = AppointmentStatuses.Scheduled, string? notes = null) => new()
        { PatientId = id, CreatedByUserId = actorId, AppointmentDate = clock.Today.AddDays(14), SlotTime = new(9, 0), Status = state, Notes = notes };
        var far = Make(owner.PatientId); var foreign = Make(other.PatientId); var checkedIn = Make(owner.PatientId, AppointmentStatuses.CheckedIn);
        var fullNote = Make(owner.PatientId, notes: new string('x', 500)); var near = Make(owner.PatientId);
        var nearTime = clock.Now.AddMinutes(30); near.AppointmentDate = DateOnly.FromDateTime(nearTime); near.SlotTime = TimeOnly.FromDateTime(nearTime);
        db.Appointments.AddRange(far, foreign, checkedIn, fullNote, near); await db.SaveChangesAsync();
        var auditCount = await db.AuditLogs.CountAsync();
        string Url(int id) => $"/api/appointments/{id}/cancellation-eligibility";
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync(Url(far.AppointmentId))) check(response.StatusCode == HttpStatusCode.Unauthorized, "Self cancel eligibility anonymous 401");
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant })
        { authenticate(role); using var response = await client.GetAsync(Url(far.AppointmentId)); check(response.StatusCode == HttpStatusCode.Forbidden, $"Self cancel eligibility {role} 403"); }
        authenticate(RoleCodes.Patient);
        using (var response = await client.GetAsync(Url(foreign.AppointmentId))) check(response.StatusCode == HttpStatusCode.NotFound, "Self cancel eligibility other account 404");
        using (var response = await client.GetAsync(Url(0))) check(response.StatusCode == HttpStatusCode.BadRequest, "Self cancel eligibility invalid ID 400");
        using (var response = await client.GetAsync(Url(far.AppointmentId)))
        {
            var data = await response.Content.ReadFromJsonAsync<SelfCancellationEligibilityResponse>();
            check(response.StatusCode == HttpStatusCode.OK && data?.CanCancel == true && data.AppointmentId == far.AppointmentId, "Self cancel eligibility owned future appointment allowed");
            check(response.Headers.CacheControl?.NoStore == true, "Self cancel eligibility no-store");
        }
        using (var response = await client.GetAsync(Url(near.AppointmentId)))
        {
            var data = await response.Content.ReadFromJsonAsync<SelfCancellationEligibilityResponse>();
            check(data?.CanCancel == false && data.Message?.Contains("2 giờ") == true, "Self cancel near deadline offers reception guidance");
        }
        using (var response = await client.GetAsync(Url(checkedIn.AppointmentId)))
            check((await response.Content.ReadFromJsonAsync<SelfCancellationEligibilityResponse>())?.CanCancel == false, "Self cancel checked-in appointment not eligible");
        using (var response = await client.GetAsync(Url(fullNote.AppointmentId)))
        {
            var data = await response.Content.ReadFromJsonAsync<SelfCancellationEligibilityResponse>();
            check(data?.CanCancel == true && data.ReasonMaxLength == 0, "Self cancel eligibility supports optional reason when note budget is zero");
        }
        check(await db.AuditLogs.CountAsync() == auditCount && await db.Appointments.AsNoTracking().Where(a => a.AppointmentId == far.AppointmentId).AllAsync(a => a.Status == AppointmentStatuses.Scheduled), "Self cancel eligibility GET never mutates or audits");
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{far.AppointmentId}/cancel", new CancelAppointmentRequest("Synthetic reason")))
            check(response.StatusCode == HttpStatusCode.OK, "Self cancel confirmed eligible request succeeds");
        using (var response = await client.GetAsync(Url(far.AppointmentId)))
            check((await response.Content.ReadFromJsonAsync<SelfCancellationEligibilityResponse>())?.CanCancel == false, "Self cancel eligibility reflects committed cancellation");
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{far.AppointmentId}/cancel", new CancelAppointmentRequest()))
            check(response.StatusCode == HttpStatusCode.BadRequest, "Self cancel replay cannot write another cancellation");
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{fullNote.AppointmentId}/cancel", new CancelAppointmentRequest()))
            check(response.StatusCode == HttpStatusCode.OK && (await db.Appointments.AsNoTracking().SingleAsync(a => a.AppointmentId == fullNote.AppointmentId)).Notes?.Length == 500, "Self cancel blank reason preserves full original notes");
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{near.AppointmentId}/cancel", new CancelAppointmentRequest()))
            check(response.StatusCode == HttpStatusCode.BadRequest, "Self cancel write still rejects ineligible deadline");
        var ids = new[] { far.AppointmentId, fullNote.AppointmentId };
        check(await db.AuditLogs.CountAsync(a => a.Action == AuditActions.AppointmentCancelled && ids.Contains(a.EntityId ?? 0)) == 2 &&
            (await db.Appointments.AsNoTracking().SingleAsync(a => a.AppointmentId == near.AppointmentId)).Status == AppointmentStatuses.Scheduled, "Self cancel audit only for two successful writes, near appointment unchanged");
    }
}
