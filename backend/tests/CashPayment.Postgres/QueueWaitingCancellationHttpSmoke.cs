using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class QueueWaitingCancellationHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Cancellation fixtures require the owned isolated database.");
        string Path(int id) => $"/api/queue/{id}/cancel-waiting";
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.PostAsJsonAsync(Path(1), new { confirmed = true })) check(response.StatusCode == HttpStatusCode.Unauthorized, "Cancel waiting anonymous401");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role); using var response = await client.PostAsJsonAsync(Path(1), new { confirmed = true });
            check(response.StatusCode == HttpStatusCode.Forbidden, $"Cancel waiting {role}403");
        }
        authenticate(RoleCodes.Admin);
        using (var response = await client.PostAsJsonAsync(Path(0), new { confirmed = true })) check(response.StatusCode == HttpStatusCode.BadRequest, "Cancel waiting invalid id400");
        using (var response = await client.PostAsJsonAsync(Path(1), new { confirmed = false })) check(response.StatusCode == HttpStatusCode.BadRequest, "Cancel waiting requires confirmation400");
        using (var response = await client.PostAsJsonAsync(Path(1), new { confirmed = true, reason = new string('x', 501) })) check(response.StatusCode == HttpStatusCode.BadRequest, "Cancel waiting reason501400");
        using (var response = await client.PostAsJsonAsync(Path(int.MaxValue), new { confirmed = true })) check(response.StatusCode == HttpStatusCode.NotFound, "Cancel waiting missing404");
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist })
        {
            var patient = new Patient { FullName = "Synthetic waiting cancellation", DateOfBirth = new(1990, 1, 1), Phone = "0999999941" };
            var appointment = new Appointment { Patient = patient, AppointmentDate = new VietnamClock(TimeProvider.System).Today,
                SlotTime = new(10, 0), Status = AppointmentStatuses.CheckedIn, CreatedByUserId = actorId };
            var entry = new QueueEntry { Patient = patient, Appointment = appointment, Visit = new Visit { Patient = patient, Status = VisitStatuses.Created, CreatedByUserId = actorId },
                QueueDate = appointment.AppointmentDate, Status = QueueStatus.Waiting, CheckInTime = DateTime.UtcNow,
                QueueNumber = (await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0) + 1 };
            db.QueueEntries.Add(entry); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            authenticate(role);
            using (var response = await client.PostAsJsonAsync(Path(entry.QueueEntryId), new { confirmed = true, reason = "  Synthetic cancellation  " }))
                check(response.StatusCode == HttpStatusCode.OK, $"Cancel waiting {role}200");
            var stored = await db.QueueEntries.AsNoTracking().Include(q => q.Visit).Include(q => q.Appointment).SingleAsync(q => q.QueueEntryId == entry.QueueEntryId);
            check(stored.Status == QueueStatus.Cancelled && stored.Visit!.Status == VisitStatuses.Cancelled && stored.Visit.EndedAt.HasValue && stored.Visit.StartedAt == null,
                "Cancel waiting persists queue and Visit together");
            check(stored.Appointment!.Status == AppointmentStatuses.CheckedIn, "Cancel waiting preserves attended appointment history");
            var history = await db.QueueStatusHistories.AsNoTracking().SingleAsync(h => h.QueueEntryId == entry.QueueEntryId);
            check(history.FromStatus == QueueStatus.Waiting && history.ToStatus == QueueStatus.Cancelled && history.ChangedByUserId == actorId && history.Reason == "Synthetic cancellation",
                "Cancel waiting history stores actual actor and trimmed reason");
            var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == "QueueEntry" && a.EntityId == entry.QueueEntryId && a.Action == AuditActions.QueueStatusChanged);
            check(audit.UserId == actorId && audit.Detail!.Contains("CancelWaiting") && !audit.Detail.Contains(patient.FullName) && !audit.Detail.Contains(patient.Phone) && !audit.Detail.Contains("Synthetic cancellation"),
                "Cancel waiting audit excludes patient and reason text");
            using (var response = await client.PostAsJsonAsync(Path(entry.QueueEntryId), new { confirmed = true }))
                check(response.StatusCode == HttpStatusCode.Conflict, "Cancel waiting repeated request409");
            check(await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == entry.QueueEntryId) == 1,
                "Cancel waiting repeat adds no history");
            using (var response = await client.PostAsJsonAsync("/api/queue/check-in", new { patientId = patient.PatientId }))
                check(response.StatusCode == HttpStatusCode.Created, "Cancel waiting releases open Visit so patient can check in again");
        }
        var active = await db.QueueEntries.Include(q => q.Visit).FirstAsync(q => q.Status == QueueStatus.InConsultation);
        var before = active.Visit!.Status;
        authenticate(RoleCodes.Admin);
        using (var response = await client.PostAsJsonAsync(Path(active.QueueEntryId), new { confirmed = true }))
            check(response.StatusCode == HttpStatusCode.Conflict, "Cancel waiting rejects already called Visit409");
        check((await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == active.VisitId)).Status == before,
            "Cancel waiting rejection never cancels clinical Visit");
    }
}
