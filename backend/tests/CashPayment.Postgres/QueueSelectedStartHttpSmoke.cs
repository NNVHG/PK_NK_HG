using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class QueueSelectedStartHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        Action<string> authenticate, Action<string, int> authenticateActor, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Selected start fixtures require owned database.");
        var role = await db.Roles.SingleAsync(r => r.RoleCode == RoleCodes.Dentist);
        var dentist = new User { RoleId = role.RoleId, IsActive = true, FullName = "Synthetic selected dentist",
            Phone = "START" + Guid.NewGuid().ToString("N")[..14], PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
        db.Users.Add(dentist); await db.SaveChangesAsync(); await TestChairFixtures.EnsureAsync(db, dentist.UserId, actorId);
        async Task<QueueEntry> Waiting()
        {
            var patient = new Patient { FullName = "Synthetic selected patient", Phone = "0999999931", DateOfBirth = new(1990, 1, 1) };
            var row = new QueueEntry { Patient = patient, Visit = new Visit { Patient = patient, CreatedByUserId = actorId, Status = VisitStatuses.Created },
                QueueDate = new VietnamClock(SmokeTestTimeProvider.Instance).Today, QueueNumber = (await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0) + 1,
                CheckInTime = DateTime.UtcNow, Status = QueueStatus.Waiting };
            db.QueueEntries.Add(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return row;
        }
        var entry = await Waiting(); string Path(int id) => $"/api/queue/{id}/status";
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.PutAsJsonAsync(Path(entry.QueueEntryId), new { newStatus = 2, dentistId = dentist.UserId }))
            check(r.StatusCode == HttpStatusCode.Unauthorized, "Selected start anonymous401");
        foreach (var denied in new[] { RoleCodes.Patient, RoleCodes.Receptionist, RoleCodes.Assistant })
        { authenticate(denied); using var r = await client.PutAsJsonAsync(Path(entry.QueueEntryId), new { newStatus = 2, dentistId = dentist.UserId }); check(r.StatusCode == HttpStatusCode.Forbidden, $"Selected start {denied}403"); }
        authenticate(RoleCodes.Admin);
        using (var r = await client.PutAsJsonAsync(Path(entry.QueueEntryId), new { newStatus = 2, dentistId = 0 })) check(r.StatusCode == HttpStatusCode.BadRequest, "Selected start dentist0 validation400");
        using (var r = await client.PutAsJsonAsync(Path(entry.QueueEntryId), new { newStatus = 2, dentistId = actorId })) check(r.StatusCode == HttpStatusCode.BadRequest, "Selected start non-dentist400");
        authenticateActor(RoleCodes.Dentist, dentist.UserId);
        using (var r = await client.PutAsJsonAsync(Path(entry.QueueEntryId), new { newStatus = 2, dentistId = actorId })) check(r.StatusCode == HttpStatusCode.Forbidden, "Selected start dentist cannot spoof403");
        authenticate(RoleCodes.Admin);
        using (var r = await client.PutAsJsonAsync(Path(entry.QueueEntryId), new { newStatus = 2, dentistId = dentist.UserId })) check(r.StatusCode == HttpStatusCode.OK, "Selected start Admin chosen dentist200");
        var persisted = await db.QueueEntries.AsNoTracking().Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == entry.QueueEntryId);
        check(persisted.Status == QueueStatus.InConsultation && persisted.DentistId == dentist.UserId &&
            persisted.Visit!.Status == VisitStatuses.InProgress && persisted.Visit.DentistId == dentist.UserId && persisted.Visit.StartedAt.HasValue,
            "Selected start persists Queue and Visit with chosen dentist");
        check(await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == entry.QueueEntryId && h.ChangedByUserId == actorId) == 1,
            "Selected start history records actual Admin actor");
        var another = await Waiting(); authenticateActor(RoleCodes.Dentist, dentist.UserId);
        using (var r = await client.PutAsJsonAsync(Path(another.QueueEntryId), new { newStatus = 2 })) check(r.StatusCode == HttpStatusCode.Conflict, "Selected start active dentist blocked409");
        using (var r = await client.PostAsJsonAsync($"/api/queue/{entry.QueueEntryId}/cancel-waiting", new { confirmed = true })) check(r.StatusCode == HttpStatusCode.Forbidden, "Selected start dentist cannot use reception cancellation");
    }
}
