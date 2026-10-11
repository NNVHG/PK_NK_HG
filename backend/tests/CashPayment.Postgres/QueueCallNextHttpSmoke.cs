using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class QueueCallNextHttpSmoke
{
    public static async Task RunAsync(
        HttpClient client,
        DbContextOptions<DentalDbContext> options,
        int actorId,
        Action<string> authenticate,
        Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Kiểm tra màn hình TV công khai: AllowAnonymous, no-store, bảo vệ PII
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync("/api/queue/display"))
        {
            check(response.StatusCode == HttpStatusCode.OK, "Queue display anonymous 200");
            check(response.Headers.CacheControl?.NoStore == true, "Queue display no-store");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(json.RootElement.TryGetProperty("inConsultation", out _), "Queue display has inConsultation");
            check(json.RootElement.TryGetProperty("waiting", out _), "Queue display has waiting");
        }

        using (var response = await client.GetAsync("/api/queue/public-monitor"))
        {
            check(response.StatusCode == HttpStatusCode.OK, "Queue public-monitor anonymous 200");
        }

        // 2. Kiểm tra phân quyền gọi tiếp theo POST /api/queue/call-next
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.PostAsJsonAsync("/api/queue/call-next", new { }))
        {
            check(response.StatusCode == HttpStatusCode.Unauthorized, "Queue call-next anonymous 401");
        }

        authenticate(RoleCodes.Patient);
        using (var response = await client.PostAsJsonAsync("/api/queue/call-next", new { }))
        {
            check(response.StatusCode == HttpStatusCode.Forbidden, "Queue call-next Patient 403");
        }

        authenticate(RoleCodes.Receptionist);
        using (var response = await client.PostAsJsonAsync("/api/queue/call-next", new { }))
        {
            check(response.StatusCode == HttpStatusCode.Forbidden, "Queue call-next Receptionist 403");
        }

        // 3. Chuẩn bị dữ liệu ca chờ để Nha sĩ gọi vào khám
        var dentist = await db.Users.FirstOrDefaultAsync(u => u.Role != null && u.Role.RoleCode == RoleCodes.Dentist && u.IsActive);
        if (dentist == null)
        {
            var role = await db.Roles.FirstAsync(r => r.RoleCode == RoleCodes.Dentist);
            dentist = new User
            {
                FullName = "BS. Smoke Test",
                Phone = "0988776655",
                PasswordHash = "dummy",
                RoleId = role.RoleId,
                IsActive = true
            };
            db.Users.Add(dentist);
            await db.SaveChangesAsync();
        }

        // Hủy bất kỳ ca InProgress nào của nha sĩ này trước đó để không bị chặn DL-045
        var activeVisits = await db.Visits.Where(v => v.DentistId == dentist.UserId && v.Status == VisitStatuses.InProgress).ToListAsync();
        foreach (var v in activeVisits) { v.Status = VisitStatuses.Completed; }

        var startNum = await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0;
        var patient = new Patient { FullName = "Bùi Văn Kiểm Thử", DateOfBirth = new(1995, 5, 5), Phone = "0911223344" };
        var visit = new Visit { Patient = patient, CreatedByUserId = actorId, Status = VisitStatuses.Created };
        var entry = new QueueEntry
        {
            Patient = patient,
            Visit = visit,
            QueueDate = today,
            QueueNumber = startNum + 10,
            CheckInTime = DateTime.UtcNow,
            Status = QueueStatus.Waiting,
            IsPriority = false
        };
        db.QueueEntries.Add(entry);
        await db.SaveChangesAsync();

        // 4. Nha sĩ gọi số tiếp theo thành công
        authenticate(RoleCodes.Dentist);
        using (var response = await client.PostAsJsonAsync("/api/queue/call-next", new { }))
        {
            check(response.StatusCode == HttpStatusCode.OK, "Queue call-next Dentist 200");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(json.RootElement.GetProperty("status").GetInt32() == (int)QueueStatus.InConsultation, "Queue call-next status is InConsultation");
            check(json.RootElement.GetProperty("dentistId").GetInt32() > 0, "Queue call-next assigned dentistId");
        }

        // 5. Kiểm tra lại /api/queue/display thấy ca đang khám có tên được che
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync("/api/queue/display"))
        {
            check(response.StatusCode == HttpStatusCode.OK, "Queue display after call 200");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var currentItems = json.RootElement.GetProperty("inConsultation").EnumerateArray().ToList();
            var matched = currentItems.FirstOrDefault(x => x.GetProperty("queueEntryId").GetInt32() == entry.QueueEntryId);
            check(matched.ValueKind != JsonValueKind.Undefined, "Called patient appears in display inConsultation");
            if (matched.ValueKind != JsonValueKind.Undefined)
            {
                var masked = matched.GetProperty("maskedPatientName").GetString() ?? "";
                check(masked.Contains("***"), $"Display masked name format ({masked})");
            }
        }
    }
}
