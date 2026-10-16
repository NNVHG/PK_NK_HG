using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Queue.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

internal static class QueueCallNextHttpSmoke
{
    public static async Task RunAsync(
        HttpClient client,
        DbContextOptions<DentalDbContext> options,
        int actorId,
        Action<string> authenticate,
        Action<string, int> authenticateActor,
        Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Fixture gọi số chỉ được chạy trên CSDL kiểm thử do bộ chạy sở hữu.");
        var today = new VietnamClock(SmokeTestTimeProvider.Instance).Today;

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
        authenticate(RoleCodes.Admin);
        using (var response = await client.PostAsJsonAsync("/api/queue/call-next", new { }))
            check(response.StatusCode == HttpStatusCode.BadRequest, "Queue call-next Admin requires selected dentist400");
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
        var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleCode == RoleCodes.Dentist);
        if (role is null)
        {
            role = new Role { RoleCode = RoleCodes.Dentist, RoleName = "Nha sĩ kiểm thử gọi số" };
            db.Roles.Add(role);
        }
        // Dùng nha sĩ riêng để không sửa ca khám của fixture khác nhằm vượt DL-045.
        var dentist = new User
        {
            FullName = "BS. Smoke Test",
            Phone = "TVTEST" + Guid.NewGuid().ToString("N")[..14],
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
            Role = role,
            IsActive = true
        };
        db.Users.Add(dentist);
        await db.SaveChangesAsync();
        await TestChairFixtures.EnsureAsync(db, dentist.UserId, actorId);

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

        // Bộ chạy trước có thể để lại ca hẹn ưu tiên; CallNext phải giữ thứ tự thật.
        var expectedEntry = await new QueueRepository(db).GetNextWaitingEntryAsync(today, dentist.UserId);
        check(expectedEntry is not null, "Queue fixture has eligible waiting entry today");
        var expectedEntryId = expectedEntry!.QueueEntryId;
        var expectedPatientName = expectedEntry.Patient.FullName;
        var calledEntryId = 0;

        // 4. Nha sĩ gọi số tiếp theo thành công
        authenticateActor(RoleCodes.Dentist, dentist.UserId);
        using (var response = await client.PostAsJsonAsync("/api/queue/call-next", new { }))
        {
            check(response.StatusCode == HttpStatusCode.OK, "Queue call-next Dentist 200");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            calledEntryId = json.RootElement.GetProperty("queueEntryId").GetInt32();
            check(calledEntryId == expectedEntryId, "Queue call-next preserves repository priority order");
            check(json.RootElement.GetProperty("status").GetInt32() == (int)QueueStatus.Calling, "Queue call-next status is Calling without starting Visit");
            check(json.RootElement.GetProperty("dentistId").GetInt32() > 0, "Queue call-next assigned dentistId");
            check(json.RootElement.GetProperty("dentistId").GetInt32() == dentist.UserId, "Queue call-next assigns actual JWT dentist");
        }
        check(await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == calledEntryId &&
            h.ToStatus == QueueStatus.Calling && h.ChangedByUserId == dentist.UserId), "Queue call-next history records actual JWT dentist");
        using (var response = await client.PostAsJsonAsync("/api/queue/call-next", new { }))
            check(response.StatusCode == HttpStatusCode.Conflict, "Queue call-next pending invitation blocks another call409");

        // 5. Kiểm tra lại /api/queue/display thấy ca đang khám có tên được che
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync("/api/queue/display"))
        {
            check(response.StatusCode == HttpStatusCode.OK, "Queue display after call 200");
            check(response.Headers.CacheControl?.NoStore == true, "Queue display after call no-store");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var currentItems = json.RootElement.GetProperty("inConsultation").EnumerateArray().ToList();
            var matched = currentItems.FirstOrDefault(x => x.GetProperty("queueEntryId").GetInt32() == calledEntryId);
            check(matched.ValueKind != JsonValueKind.Undefined, "Called patient appears in display inConsultation");
            if (matched.ValueKind != JsonValueKind.Undefined)
            {
                var masked = matched.GetProperty("maskedPatientName").GetString() ?? "";
                check(masked.Contains("***"), "Display masked name format");
                check(masked == QueueService.MaskPatientName(expectedPatientName) && masked != expectedPatientName,
                    "Display masks name of actual called patient");
                check(!matched.TryGetProperty("patientName", out _) && !matched.TryGetProperty("patientPhone", out _) &&
                    !matched.TryGetProperty("patientId", out _) && !matched.TryGetProperty("patientCode", out _) &&
                    !matched.TryGetProperty("notes", out _), "Display excludes patient identity/contact/clinical fields");
            }
        }

        authenticateActor(RoleCodes.Dentist, dentist.UserId);
        using (var response = await client.PutAsJsonAsync($"/api/queue/{calledEntryId}/status", new { newStatus = 2, dentistId = dentist.UserId }))
            check(response.StatusCode == HttpStatusCode.OK, "Invited entry requires explicit clinical start200");
        using (var response = await client.PostAsJsonAsync("/api/queue/call-next", new { }))
            check(response.StatusCode == HttpStatusCode.Conflict, "Active clinical Visit still blocks a different patient409");

        // Real HTTP recall of the same Visit after the existing imaging-return transition.
        db.ChangeTracker.Clear();
        var returning = await db.QueueEntries.Include(q => q.Visit).SingleAsync(q => q.QueueEntryId == calledEntryId);
        var startedAt = returning.Visit!.StartedAt;
        returning.Status = QueueStatus.Waiting;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        authenticate(RoleCodes.Admin);
        using (var response = await client.PostAsJsonAsync("/api/queue/call-next", new { dentistId = dentist.UserId }))
        {
            check(response.StatusCode == HttpStatusCode.OK, "Queue call-next Admin selected dentist resumes imaging return200");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(json.RootElement.GetProperty("queueEntryId").GetInt32() == calledEntryId &&
                json.RootElement.GetProperty("dentistId").GetInt32() == dentist.UserId, "Queue recall keeps same queue entry and dentist");
        }
        check((await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == returning.VisitId)).StartedAt == startedAt,
            "Queue HTTP recall preserves original StartedAt");
        dentist = await db.Users.SingleAsync(u => u.UserId == dentist.UserId); dentist.IsActive = false;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        authenticateActor(RoleCodes.Dentist, dentist.UserId);
        using (var response = await client.PostAsJsonAsync("/api/queue/call-next", new { }))
            check(response.StatusCode == HttpStatusCode.Forbidden, "Queue call-next inactive JWT dentist403");
    }
}
