using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Patients.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
internal static class FamilyBookingHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, int unownedId,
        Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var original = await db.Patients.AsNoTracking().SingleAsync(p => p.UserId == actorId);
        var second = new Patient { UserId = actorId, FullName = "Synthetic family child", DateOfBirth = new(2020, 1, 1), Phone = original.Phone };
        var inactive = new Patient { UserId = actorId, FullName = "Synthetic inactive profile", DateOfBirth = new(1990, 1, 1), Phone = original.Phone, IsActive = false };
        db.Patients.AddRange(second, inactive); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        check(await db.Patients.CountAsync(p => p.UserId == actorId) == 3, "Family migration permits multiple profiles per User");
        check((await db.Patients.AsNoTracking().SingleAsync(p => p.PatientId == original.PatientId)).FullName == original.FullName, "Adding family members preserves original profile data");
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync("/api/patients/mine")) check(response.StatusCode == HttpStatusCode.Unauthorized, "Family profiles anonymous 401");
        authenticate(RoleCodes.Patient);
        using (var response = await client.GetAsync("/api/patients/mine"))
        {
            var profiles = await response.Content.ReadFromJsonAsync<PatientListItemResponse[]>();
            check(response.StatusCode == HttpStatusCode.OK && profiles?.Length == 2 && profiles.Any(p => p.PatientId == original.PatientId) &&
                profiles.Any(p => p.PatientId == second.PatientId && p.DateOfBirth == second.DateOfBirth), "Family mine returns both active owned profiles with date of birth");
            check(profiles!.All(p => p.PatientId != inactive.PatientId && p.PatientId != unownedId), "Family mine excludes inactive and unrelated profiles");
            check(response.Headers.CacheControl?.NoStore == true, "Family mine no-store");
        }
        var date = new VietnamClock(SmokeTestTimeProvider.Instance).Today.AddDays(2);
        foreach (var id in new[] { original.PatientId, second.PatientId })
        {
            using var response = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(id, date, new(8, 0)));
            var appointment = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
            check(response.StatusCode == HttpStatusCode.Created && appointment?.PatientId == id, "Family Patient books selected member, including same day for different member");
        }
        using (var response = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(unownedId, date, new(8, 30))))
            check(response.StatusCode == HttpStatusCode.NotFound, "Family booking another account profile 404");
        using (var response = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(inactive.PatientId, date, new(8, 30))))
            check(response.StatusCode == HttpStatusCode.BadRequest, "Family booking inactive member rejected");
        using (var response = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(second.PatientId, date, new(8, 30))))
            check(response.StatusCode == HttpStatusCode.Conflict, "Family duplicate appointment for same member remains blocked");
        check(await db.Appointments.CountAsync(a => a.CreatedByUserId == actorId && a.AppointmentDate == date) == 2,
            "Family rejected requests never create appointments");
        authenticate(RoleCodes.Receptionist);
        using (var response = await client.GetAsync("/api/patients/mine")) check(response.StatusCode == HttpStatusCode.Forbidden, "Family mine staff cannot impersonate patient account");
        var owner = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == actorId);
        await using var failing = new DentalDbContext(options);
        failing.Users.Add(new User { RoleId = owner.RoleId, Phone = owner.Phone, FullName = "Synthetic duplicate account", PasswordHash = "test-only" });
        try { await failing.SaveChangesAsync(); throw new InvalidOperationException("Expected unique User phone violation."); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        { check(true, "Family User phone uniqueness remains enforced in PostgreSQL"); }
    }
}
