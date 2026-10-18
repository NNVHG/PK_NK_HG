using System.Net;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class DailyQueueHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var today = new VietnamClock(SmokeTestTimeProvider.Instance).Today;
        var start = await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0;
        var rows = new List<QueueEntry>();
        for (var i = 0; i < 6; i++)
        {
            var patient = new Patient { FullName = $"Synthetic queue {i}", DateOfBirth = new(1990, 1, 1), Phone = $"099999996{i}" };
            var row = new QueueEntry { Patient = patient, QueueDate = i == 5 ? today.AddDays(-1) : today, QueueNumber = start + i + 1,
                IsPriority = i < 2, CheckInTime = DateTime.UtcNow.AddMinutes(-i), Status = i == 4 ? QueueStatus.Completed : QueueStatus.Waiting };
            // Check-in creates a Visit; read fixtures must also support the subsequent call-next flow.
            row.Visit = new Visit { Patient = patient, CreatedByUserId = actorId,
                Status = i == 4 ? VisitStatuses.Completed : VisitStatuses.Created, IsLocked = i == 4 };
            if (i < 2) row.Appointment = new Appointment { Patient = patient, AppointmentDate = today, SlotTime = new(9 - i, 0), CreatedByUserId = actorId, Status = AppointmentStatuses.CheckedIn };
            db.QueueEntries.Add(row); rows.Add(row);
        }
        await db.SaveChangesAsync();
        var debtVisits = Enumerable.Range(0, 5)
            .Select(_ => new Visit { PatientId = rows[0].PatientId, CreatedByUserId = actorId,
                Status = VisitStatuses.Completed, IsLocked = true })
            .ToArray();
        db.Visits.AddRange(debtVisits);
        await db.SaveChangesAsync();
        db.Invoices.AddRange(
            new Invoice { VisitId = debtVisits[0].VisitId, CreatedByUserId = actorId, InvoiceCode = "TEST-DEBT-PENDING", TotalAmount = 1000, PaidAmount = 300, Status = InvoiceStatus.PendingPayment, CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new Invoice { VisitId = debtVisits[1].VisitId, CreatedByUserId = actorId, InvoiceCode = "TEST-DEBT-PARTIAL", TotalAmount = 2000, PaidAmount = 1500, Status = InvoiceStatus.PartiallyPaid, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new Invoice { VisitId = debtVisits[2].VisitId, CreatedByUserId = actorId, InvoiceCode = "TEST-DEBT-DRAFT", TotalAmount = 3000, Status = InvoiceStatus.Draft, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new Invoice { VisitId = debtVisits[3].VisitId, CreatedByUserId = actorId, InvoiceCode = "TEST-DEBT-CANCELLED", TotalAmount = 4000, Status = InvoiceStatus.Cancelled, CreatedAt = DateTime.UtcNow },
            new Invoice { VisitId = debtVisits[4].VisitId, CreatedByUserId = actorId, InvoiceCode = "TEST-DEBT-PAID", TotalAmount = 5000, PaidAmount = 5000, Status = InvoiceStatus.Paid, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var audits = await db.AuditLogs.CountAsync();
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync("/api/queue")) check(response.StatusCode == HttpStatusCode.Unauthorized, "Queue list anonymous 401");
        authenticate(RoleCodes.Patient);
        using (var response = await client.GetAsync("/api/queue")) check(response.StatusCode == HttpStatusCode.Forbidden, "Queue list Patient 403");
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role);
            using var response = await client.GetAsync("/api/queue?status=1&pageSize=100");
            check(response.StatusCode == HttpStatusCode.OK, $"Queue list {role} 200");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var ids = json.RootElement.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("queueEntryId").GetInt32()).Where(id => rows.Any(r => r.QueueEntryId == id)).ToArray();
            check(ids.SequenceEqual(new[] { rows[1].QueueEntryId, rows[0].QueueEntryId, rows[3].QueueEntryId, rows[2].QueueEntryId }), $"Queue {role} priority by slot then ordinary by check-in, today/status filter");
            check(response.Headers.CacheControl?.NoStore == true, $"Queue {role} no-store");
        }
        using (var response = await client.GetAsync("/api/queue?status=1&pageSize=1"))
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(response.StatusCode == HttpStatusCode.OK && json.RootElement.GetProperty("items").GetArrayLength() == 1 && json.RootElement.GetProperty("hasNextPage").GetBoolean(), "Queue bounded pagination");
        }
        foreach (var query in new[] { "status=8", "dentistId=0", "page=1000001", "pageSize=101" })
        { using var response = await client.GetAsync($"/api/queue?{query}"); check(response.StatusCode == HttpStatusCode.BadRequest, $"Queue invalid {query} 400"); }

        var debtUrl = $"/api/queue/check-in/{rows[0].PatientId}/outstanding-debt";
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync(debtUrl)) check(response.StatusCode == HttpStatusCode.Unauthorized, "Check-in debt anonymous 401");
        authenticate(RoleCodes.Patient);
        using (var response = await client.GetAsync(debtUrl)) check(response.StatusCode == HttpStatusCode.Forbidden, "Check-in debt Patient 403");
        foreach (var role in new[] { RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role);
            using var response = await client.GetAsync(debtUrl);
            check(response.StatusCode == HttpStatusCode.Forbidden, $"Check-in debt {role} 403");
        }
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist })
        {
            authenticate(role);
            using var response = await client.GetAsync(debtUrl);
            check(response.StatusCode == HttpStatusCode.OK, $"Check-in debt {role} 200");
            check(response.Headers.CacheControl?.NoStore == true, $"Check-in debt {role} no-store");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var codes = json.RootElement.GetProperty("invoiceCodes").EnumerateArray().Select(code => code.GetString()).ToArray();
            check(json.RootElement.GetProperty("totalOutstandingDebt").GetDecimal() == 1200m &&
                  codes.SequenceEqual(new[] { "TEST-DEBT-PENDING", "TEST-DEBT-PARTIAL" }),
                $"Check-in debt {role} only unpaid positive balances");
        }
        authenticate(RoleCodes.Receptionist);
        using (var response = await client.GetAsync($"/api/queue/check-in/{rows[1].PatientId}/outstanding-debt"))
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(response.StatusCode == HttpStatusCode.OK && json.RootElement.GetProperty("totalOutstandingDebt").GetDecimal() == 0m &&
                  json.RootElement.GetProperty("invoiceCodes").GetArrayLength() == 0, "Check-in debt no open balance returns empty summary");
        }
        using (var response = await client.GetAsync("/api/queue/check-in/0/outstanding-debt"))
            check(response.StatusCode == HttpStatusCode.BadRequest, "Check-in debt nonpositive patient id 400");
        check(await db.AuditLogs.CountAsync() == audits && await db.QueueEntries.CountAsync(q => rows.Select(r => r.QueueEntryId).Contains(q.QueueEntryId)) == 6, "Queue and debt GET never change queue or audit");
    }
}
