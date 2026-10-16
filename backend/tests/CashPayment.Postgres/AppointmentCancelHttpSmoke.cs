using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

internal static class AppointmentCancelHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var clock = new VietnamClock(SmokeTestTimeProvider.Instance);
        var date = clock.Today.AddDays(8);
        var patient = new Patient { FullName = "Synthetic cancellation", DateOfBirth = new(1990, 1, 1), Phone = "0999999941" };
        db.Patients.Add(patient); await db.SaveChangesAsync();
        var owner = await db.Patients.FirstAsync(p => p.UserId == actorId && p.IsActive);
        Appointment Make(int patientId, string state = AppointmentStatuses.Scheduled, string? notes = null) => new()
        { PatientId = patientId, AppointmentDate = date, SlotTime = new(9, 0), CreatedByUserId = actorId, Status = state, Notes = notes };
        var staffRows = new[] { Make(patient.PatientId), Make(patient.PatientId) };
        var own = Make(owner.PatientId); var other = Make(patient.PatientId); var checkedIn = Make(patient.PatientId, AppointmentStatuses.CheckedIn);
        var fullNote = Make(patient.PatientId, notes: new string('x', 500));
        var near = Make(owner.PatientId); var nearTime = clock.Now.AddMinutes(30); near.AppointmentDate = DateOnly.FromDateTime(nearTime); near.SlotTime = TimeOnly.FromDateTime(nearTime);
        db.Appointments.AddRange(staffRows.Concat(new[] { own, other, checkedIn, fullNote, near })); await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync($"/api/appointments/desk?date={date:yyyy-MM-dd}")) check(response.StatusCode == HttpStatusCode.Unauthorized, "Appointment desk anonymous 401");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role); using var response = await client.GetAsync("/api/appointments/desk");
            check(response.StatusCode == HttpStatusCode.Forbidden, $"Appointment desk {role} 403");
        }
        var roles = new[] { RoleCodes.Receptionist, RoleCodes.Admin };
        for (var i = 0; i < roles.Length; i++)
        {
            authenticate(roles[i]);
            using (var list = await client.GetAsync($"/api/appointments/desk?date={date:yyyy-MM-dd}&status=Scheduled&pageSize=2"))
            {
                check(list.StatusCode == HttpStatusCode.OK && list.Headers.CacheControl?.NoStore == true, $"Appointment desk {roles[i]} 200/no-store");
                using var json = System.Text.Json.JsonDocument.Parse(await list.Content.ReadAsStringAsync());
                check(json.RootElement.GetProperty("items").GetArrayLength() == 2 && json.RootElement.GetProperty("hasNextPage").GetBoolean(), "Appointment desk bounded list pagination");
            }
            var before = await new AppointmentRepository(db).GetSlotBookingCountAsync(date, new(9, 0));
            using var cancelled = await client.PutAsJsonAsync($"/api/appointments/{staffRows[i].AppointmentId}/cancel", new CancelAppointmentRequest("Synthetic reason"));
            var dto = await cancelled.Content.ReadFromJsonAsync<AppointmentResponse>();
            check(cancelled.StatusCode == HttpStatusCode.OK && dto?.Status == AppointmentStatuses.Cancelled, $"Appointment {roles[i]} cancels scheduled appointment");
            check(await new AppointmentRepository(db).GetSlotBookingCountAsync(date, new(9, 0)) == before - 1, "Appointment cancellation releases counted slot");
            var stored = await db.Appointments.AsNoTracking().SingleAsync(a => a.AppointmentId == staffRows[i].AppointmentId);
            check(stored.Notes == "[Lý do hủy: Synthetic reason]" && stored.CreatedByUserId == actorId && stored.UpdatedAt != null, "Appointment cancelled state/reason persisted without deleting row");
        }
        foreach (var role in new[] { RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role); using var response = await client.PutAsJsonAsync($"/api/appointments/{other.AppointmentId}/cancel", new CancelAppointmentRequest());
            check(response.StatusCode == HttpStatusCode.Forbidden, $"Appointment {role} cannot cancel");
        }
        authenticate(RoleCodes.Patient);
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{other.AppointmentId}/cancel", new CancelAppointmentRequest())) check(response.StatusCode == HttpStatusCode.NotFound, "Appointment Patient other account 404");
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{near.AppointmentId}/cancel", new CancelAppointmentRequest())) check(response.StatusCode == HttpStatusCode.BadRequest, "Appointment Patient under two hours rejected");
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{own.AppointmentId}/cancel", new CancelAppointmentRequest())) check(response.StatusCode == HttpStatusCode.OK, "Appointment Patient cancels owned future appointment");
        authenticate(RoleCodes.Receptionist);
        foreach (var id in new[] { checkedIn.AppointmentId, staffRows[0].AppointmentId })
        { using var response = await client.PutAsJsonAsync($"/api/appointments/{id}/cancel", new CancelAppointmentRequest()); check(response.StatusCode == HttpStatusCode.BadRequest, "Appointment already checked-in/cancelled cannot cancel"); }
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{fullNote.AppointmentId}/cancel", new CancelAppointmentRequest("Synthetic"))) check(response.StatusCode == HttpStatusCode.BadRequest, "Appointment combined note limit 400, not database error");
        using (var response = await client.GetAsync("/api/appointments/desk?status=Unknown")) check(response.StatusCode == HttpStatusCode.BadRequest, "Appointment unknown filter 400");
        using (var response = await client.GetAsync("/api/appointments/desk?page=1000001")) check(response.StatusCode == HttpStatusCode.BadRequest, "Appointment page bound 400");
        var ids = staffRows.Select(a => a.AppointmentId).Append(own.AppointmentId).ToArray();
        check(await db.AuditLogs.CountAsync(a => a.Action == AuditActions.AppointmentCancelled && ids.Contains(a.EntityId ?? 0) && a.UserId == actorId) == 3, "Appointment cancellations record actor audit only for successful writes");
    }
}
