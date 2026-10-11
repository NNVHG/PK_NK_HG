using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class CounterBookingHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var date = new VietnamClock(TimeProvider.System).Today.AddDays(5);
        foreach (var role in new[] { RoleCodes.Receptionist, RoleCodes.Admin })
        {
            var patient = new Patient { FullName = $"Synthetic counter {role}", DateOfBirth = new(1990, 1, 1), Phone = role == RoleCodes.Admin ? "0999999971" : "0999999972" };
            db.Patients.Add(patient); await db.SaveChangesAsync();
            authenticate(role);
            using var response = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(patient.PatientId, date, new(9, 0), Notes: "Synthetic counter reason"));
            var result = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
            check(response.StatusCode == HttpStatusCode.Created && result?.PatientId == patient.PatientId, $"Counter {role} can book unlinked patient");
            var row = await db.Appointments.AsNoTracking().SingleAsync(a => a.PatientId == patient.PatientId);
            check(row.CreatedByUserId == actorId && row.Status == AppointmentStatuses.Scheduled && row.Notes == "Synthetic counter reason", $"Counter {role} persists actor and selected slot");
            check(!await db.QueueEntries.AnyAsync(q => q.PatientId == patient.PatientId), $"Counter {role} booking never checks patient in");
            using var duplicate = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(patient.PatientId, date, new(9, 30)));
            check(duplicate.StatusCode == HttpStatusCode.Conflict, $"Counter {role} duplicate same-day appointment rejected");
        }
        foreach (var role in new[] { RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role);
            using var response = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(1, date, new(9, 0)));
            check(response.StatusCode == HttpStatusCode.Forbidden, $"Counter {role} cannot book");
        }
        authenticate(RoleCodes.Receptionist);
        using var invalid = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(0, date, new(12, 0)));
        check(invalid.StatusCode == HttpStatusCode.BadRequest, "Counter invalid patient/slot 400");
        using var missing = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(int.MaxValue, date, new(9, 0)));
        check(missing.StatusCode == HttpStatusCode.NotFound, "Counter missing patient 404");
    }
}
