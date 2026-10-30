using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dental.Domain.Constants;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

internal static class FiveRoleHttpSmoke
{
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, string ownedName, Action<bool, string> check, bool cleanupProbe = false)
    {
        // Kiểm preflight âm không tạo kết nối hay chạy API ngoài phạm vi.
        foreach (var invalid in new[] { "Host=localhost;Database=dental_db", "Host=example.invalid;Database=" + ownedName,
                     "Host=localhost;Database=dental_cash_test_" + new string('0', 32) })
        {
            var rejected = false;
            try { OwnedSmokeApi.ValidateDatabase(invalid, ownedName); } catch (InvalidOperationException) { rejected = true; }
            check(rejected, "Preflight rejects unowned or non-local database");
        }
        var host = new OwnedSmokeApi(options, ownedName);
        var probeObserved = false;
        try
        {
            using var client = await host.StartAsync();
            check(true, "Owned API started on its allocated loopback port and catalog health passed");
            if (cleanupProbe) throw new SmokeCleanupProbeException();
            await ScenarioAsync(client, host, options, check);
        }
        catch (SmokeCleanupProbeException) when (cleanupProbe) { probeObserved = true; }
        finally
        {
            await host.DisposeAsync();
            check(host.Exited && !Directory.Exists(host.Root), "Owned API process and temporary configuration cleaned");
        }
        if (cleanupProbe)
        {
            check(probeObserved, "Injected scenario failure exercised cleanup without running clinical writes");
            var failedHost = new OwnedSmokeApi(options, ownedName);
            var startupRejected = false;
            try { using var unexpected = await failedHost.StartAsync(failStartup: true); }
            catch (InvalidOperationException failure) when (failure.Message.Contains("KeyedIdentityHasher")) { startupRejected = true; }
            finally { await failedHost.DisposeAsync(); }
            check(startupRejected && failedHost.Exited && !Directory.Exists(failedHost.Root), "Failed API startup also cleans its owned process and directory");
        }
    }

    private sealed class SmokeCleanupProbeException : Exception;

    private static async Task ScenarioAsync(HttpClient client, OwnedSmokeApi host, DbContextOptions<DentalDbContext> options, Action<bool, string> check)
    {
        var tokens = new Dictionary<string, string>();
        var users = new Dictionary<string, int>();
        await using var db = new DentalDbContext(options);
        async Task<JsonElement> Api(string label, string method, string path, string? role = null, object? body = null, int expected = 200)
        {
            async Task<HttpResponseMessage> Send()
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                if (role is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens[role]);
                if (body is not null) request.Content = JsonContent.Create(body);
                return await client.SendAsync(request);
            }
            using var first = await Send();
            HttpResponseMessage response = first;
            HttpResponseMessage? retry = null;
            try
            {
                if ((int)first.StatusCode == 429 && expected != 429 && path.StartsWith("/api/auth/login"))
                {
                    check(true, "Login rate limit remains enabled; runner waits for the existing window");
                    await Task.Delay(TimeSpan.FromSeconds(61));
                    response = retry = await Send();
                }
                // Không ghi body, giấy tờ, credential, OTP hoặc token vào output kể cả khi API lỗi.
                check((int)response.StatusCode == expected, label + $" (HTTP {expected}, actual {(int)response.StatusCode})");
                if (response.Content.Headers.ContentType?.MediaType != "application/json") return default;
                return await response.Content.ReadFromJsonAsync<JsonElement>();
            }
            finally { retry?.Dispose(); }
        }
        async Task Login(string role, string identifier, string password, int? documentType = null)
        {
            var login = await Api("Login " + role, "POST", "/api/auth/login", body: new { phone = identifier, password, documentType });
            check(login.GetProperty("roleCode").GetString() == role, "Server role matches " + role);
            tokens[role] = login.GetProperty("accessToken").GetString()!;
            users[role] = login.GetProperty("userId").GetInt32();
        }
        async Task Activate(string role, string identifier, string initialPassword, int? documentType = null)
        {
            var pending = await Api("No JWT before OTP for " + role, "POST", "/api/auth/login", body: new { phone = identifier, password = initialPassword, documentType }, expected: 401);
            check(pending.GetProperty("code").GetString() == "AUTH_ACTIVATION_REQUIRED" && !pending.TryGetProperty("accessToken", out _), "Pending activation supplies no access token");
            var challenge = await Api("Request activation " + role, "POST", "/api/auth/activation/request",
                body: new { phone = identifier, password = initialPassword, documentType }, expected: 202);
            var code = challenge.GetProperty("developmentCode").GetString();
            check(code is { Length: 6 }, "Development-only OTP available inside owned test API");
            var verifyBody = new { challengeId = challenge.GetProperty("challengeId").GetGuid(), code };
            var grant = await Api("Verify activation " + role, "POST", "/api/auth/activation/verify", body: verifyBody);
            if (role == "PATIENT") check(grant.GetProperty("mustChangePassword").GetBoolean(), "New patient must change default password before portal");
            var completion = new { completionToken = grant.GetProperty("completionToken").GetString(), keepDefaultPassword = false, newPassword = host.Password, confirmed = true };
            await Api("Complete activation " + role, "POST", "/api/auth/activation/complete", body: completion);
            await Api("Reject OTP replay " + role, "POST", "/api/auth/activation/verify", body: verifyBody, expected: 400);
            await Api("Reject activation grant replay " + role, "POST", "/api/auth/activation/complete", body: completion, expected: 400);
            await Login(role, identifier, host.Password, documentType);
        }
        decimal Money(JsonElement element) => element.ValueKind == JsonValueKind.String
            ? decimal.Parse(element.GetString()!, CultureInfo.InvariantCulture) : element.GetDecimal();

        await Login("ADMIN", host.AdminPhone, host.Password);
        check(await db.Users.CountAsync() == 1 && await db.Users.AnyAsync(u => u.UserId == users["ADMIN"] && u.Phone == host.AdminPhone),
            "API seed is visible in the exact owned database; no inherited demo users");
        var handler = new JwtSecurityTokenHandler();
        handler.ValidateToken(tokens["ADMIN"], new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "DentalClinic",
            ValidateAudience = true, ValidAudience = "DentalClinicClient", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(host.JwtSecret)), ClockSkew = TimeSpan.Zero }, out _);
        check(true, "API token signed with this run's generated key");
        foreach (var (role, phone) in new[] { ("RECEPTIONIST", "0999999002"), ("DENTIST", "0999999003"), ("ASSISTANT", "0999999004") })
        {
            var staff = await Api("Admin creates " + role + " without supplied password", "POST", "/api/staff", "ADMIN",
                new { fullName = "Synthetic " + role, phone, roleCode = role }, 201);
            await Activate(role, phone, phone);
            check(staff.GetProperty("userId").GetInt32() == users[role], "Activated staff retains created identity");
        }
        await Api("Direct registration remains retired", "POST", "/api/auth/register", body: new { }, expected: 410);
        var catalog = await Api("Public booking catalog", "GET", "/api/appointment-requests/catalog");
        var today = DateOnly.Parse(catalog.GetProperty("today").GetString()!, CultureInfo.InvariantCulture);
        var localNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "SE Asia Standard Time");
        check(TimeOnly.FromDateTime(localNow.DateTime) < new TimeOnly(23, 25), "Run starts before reception cutoff; no clock or policy bypass");
        var slots = catalog.GetProperty("slots").EnumerateArray().Select(x => TimeOnly.Parse(x.GetString()!, CultureInfo.InvariantCulture)).ToArray();
        var future = slots.Where(x => x > TimeOnly.FromDateTime(localNow.AddMinutes(5).DateTime)).ToArray();
        var appointmentDate = future.Length > 0 ? today : today.AddDays(1);
        var slot = future.Length > 0 ? future[0] : slots[0];
        const string phonePatient = "0999999005", citizenId = "987654321012";
        var booking = new { intent = new { fullName = "Synthetic smoke patient", phone = phonePatient, dateOfBirth = "1990-01-01", gender = "Other",
            appointmentDate = appointmentDate.ToString("yyyy-MM-dd"), slotTime = slot.ToString("HH:mm:ss"), scheduleRevision = catalog.GetProperty("revision").GetString() },
            clientRequestId = Guid.NewGuid(), confirmed = true };
        var receipt = await Api("Guest booking without OTP or dentist", "POST", "/api/appointment-requests", body: booking, expected: 202);
        var repeated = await Api("Booking request replay", "POST", "/api/appointment-requests", body: booking, expected: 202);
        check(receipt.GetProperty("requestId").GetGuid() == repeated.GetProperty("requestId").GetGuid(), "Duplicate submit retains one booking request");
        check(await db.Users.CountAsync() == 4 && !await db.Patients.AnyAsync(), "Booking submission creates no patient account or chart");
        var list = await Api("Reception lists pending requests", "GET", "/api/appointment-requests", "RECEPTIONIST");
        var bookingId = list.GetProperty("items")[0].GetProperty("id").GetInt32();
        var approval = await Api("Reception reviews booking", "GET", $"/api/appointment-requests/{bookingId}/approval", "RECEPTIONIST");
        var approved = await Api("Reception approves booking", "POST", $"/api/appointment-requests/{bookingId}/approve", "RECEPTIONIST",
            new { expectedVersion = approval.GetProperty("version").GetInt32(), reviewStamp = approval.GetProperty("reviewStamp").GetString(), confirmedNewPatient = true, confirmedAtCounter = true, confirmed = true });
        var patientId = approved.GetProperty("patientId").GetInt32();
        var appointmentId = approved.GetProperty("appointmentId").GetInt32();
        check(!approved.GetProperty("createdAccount").GetBoolean() && await db.Users.CountAsync() == 4, "Approval defers account creation to identity-verified check-in");
        var checkIn = new { patientId, appointmentId = appointmentDate == today ? (int?)appointmentId : null, citizenId, phone = phonePatient, documentType = 1 };
        var queue = await Api(appointmentDate == today ? "Scheduled arrival checked in after identity verification" : "Early arrival checked in as walk-in; future appointment retained",
            "POST", "/api/queue/check-in", "RECEPTIONIST", checkIn, 201);
        var visitId = queue.GetProperty("visitId").GetInt32();
        var queueId = queue.GetProperty("queueEntryId").GetInt32();
        await Api("Duplicate check-in rejected", "POST", "/api/queue/check-in", "RECEPTIONIST", checkIn, 409);
        await Activate("PATIENT", citizenId, phonePatient, 1);
        var mine = await Api("Patient owned chart", "GET", "/api/patients/me", "PATIENT");
        check(mine.GetProperty("patientId").GetInt32() == patientId && await db.Patients.CountAsync(x => x.UserId == users["PATIENT"]) == 1, "Account owns the verified patient only");

        foreach (var role in new[] { "ADMIN", "RECEPTIONIST", "DENTIST", "ASSISTANT", "PATIENT" })
        {
            await Api("Own account " + role, "GET", "/api/auth/me", role);
            foreach (var path in new[] { "/api/staff", "/api/audit-logs" })
                await Api("Admin-only boundary " + role, "GET", path, role, expected: role == "ADMIN" ? 200 : 403);
            await Api("Queue boundary " + role, "GET", "/api/queue", role, expected: role == "PATIENT" ? 403 : 200);
            await Api("Service catalog boundary " + role, "GET", "/api/services", role, expected: role is "ADMIN" or "DENTIST" or "ASSISTANT" ? 200 : 403);
            await Api("Appointment scope " + role, "GET", "/api/appointments", role);
        }
        await Api("No token rejected", "GET", "/api/auth/me", expected: 401);
        tokens["BAD"] = "invalid-token";
        await Api("Malformed token rejected", "GET", "/api/auth/me", "BAD", expected: 401);
        tokens["EXPIRED"] = handler.WriteToken(new JwtSecurityToken("DentalClinic", "DentalClinicClient", new[] { new Claim("sub", users["ADMIN"].ToString()) },
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(host.JwtSecret)), SecurityAlgorithms.HmacSha256)));
        await Api("Expired signed token rejected", "GET", "/api/auth/me", "EXPIRED", expected: 401);
        await Api("Wrong password rejected", "POST", "/api/auth/login", body: new { phone = host.AdminPhone, password = "WrongSynthetic9" }, expected: 401);
        var another = await Api("Reception creates unowned chart", "POST", "/api/patients", "RECEPTIONIST",
            new { fullName = "Synthetic unrelated chart", phone = "0999999006", dateOfBirth = "1991-02-03", gender = "Other", confirmNotDuplicate = true }, 201);
        await Api("Other chart hidden from patient", "GET", "/api/patients/" + another.GetProperty("patientId").GetInt32(), "PATIENT", expected: 404);
        await Api("Assistant cannot create charts", "POST", "/api/patients", "ASSISTANT", new { }, 403);
        var workspace = await Api("Dentist chooses workspace options", "GET", "/api/workspaces/options", "DENTIST");
        var chairId = workspace.GetProperty("chairs").EnumerateArray().First(x => x.GetProperty("isActive").GetBoolean() && x.GetProperty("status").GetInt32() == 1).GetProperty("id").GetInt32();
        await Api("Dentist selects available workspace", "PUT", "/api/workspaces/" + users["DENTIST"], "DENTIST", new { chairId, expectedVersion = 0, confirmed = true });
        await Api("Assistant follows dentist workspace", "PUT", "/api/workspaces/" + users["ASSISTANT"], "ASSISTANT", new { dentistId = users["DENTIST"], expectedVersion = 0, confirmed = true });
        await Api("Assistant records history", "POST", $"/api/visits/{visitId}/medical-history", "ASSISTANT",
            new { items = new[] { new { type = "Allergy", name = "Synthetic test allergen", isCritical = true, detail = "Test only" } } }, 201);
        await Api("Dentist reads safety alerts", "GET", $"/api/patients/{patientId}/safety-alerts", "DENTIST");
        await Api("Assistant records vitals", "POST", $"/api/visits/{visitId}/vital-signs", "ASSISTANT", new { systolicBp = 120, diastolicBp = 80, pulseBpm = 70, temperatureC = 36.5 }, 201);
        await Api("Invalid vital rejected", "POST", $"/api/visits/{visitId}/vital-signs", "ASSISTANT", new { pulseBpm = -1 }, 400);
        await Api("Dentist starts visit", "PUT", $"/api/queue/{queueId}/status", "DENTIST", new { newStatus = 2, dentistId = users["DENTIST"] });
        await Api("Dentist records diagnosis", "PUT", $"/api/visits/{visitId}/diagnosis", "DENTIST", new { diagnosis = "Synthetic diagnosis", clinicalNotes = "Test only" });
        foreach (var tooth in new[] { 11, 51 })
            await Api("Dentist records valid FDI tooth", "POST", $"/api/visits/{visitId}/tooth-conditions", "DENTIST", new { toothNumber = tooth, surface = "B", conditionCode = "CARIES" }, 201);
        await Api("Invalid FDI tooth rejected", "POST", $"/api/visits/{visitId}/tooth-conditions", "DENTIST", new { toothNumber = 19, surface = "B", conditionCode = "CARIES" }, 400);
        var service = await Api("Admin creates priced service", "POST", "/api/services", "ADMIN",
            new { code = "SMOKE_SERVICE", name = "Synthetic service", durationMinutes = 30, initialPrice = 100000, effectiveFrom = DateTimeOffset.UtcNow }, 201);
        var serviceId = service.GetProperty("dentalServiceId").GetInt32();
        await Api("Dentist assigns service on two teeth", "POST", $"/api/visits/{visitId}/services", "DENTIST", new { serviceId, toothNumbers = new[] { 11, 21 }, quantity = 1, surface = "B" }, 201);
        var invoice = await Api("Reception reads generated draft", "GET", $"/api/invoices/by-visit/{visitId}", "RECEPTIONIST");
        var invoiceId = invoice.GetProperty("id").GetInt32();
        check(Money(invoice.GetProperty("totalAmount")) == 200000 && invoice.GetProperty("items").GetArrayLength() == 2, "Draft captures service price snapshots");
        var medicine = await Api("Admin creates synthetic medicine", "POST", "/api/medicines", "ADMIN",
            new { code = "SMOKE_MED", name = "Synthetic test medicine", unit = "Viên", unitPrice = 1000, isActive = true }, 201);
        var lines = new[] { new { medicineId = medicine.GetProperty("id").GetInt32(), medicineVersion = medicine.GetProperty("version").GetInt32(), quantity = 2, usageInstructions = "Synthetic test only" } };
        var preview = await Api("Dentist previews prescription", "POST", $"/api/visits/{visitId}/prescription/preview", "DENTIST", new { expectedVersion = 0, diagnosis = "Synthetic diagnosis", items = lines });
        await Api("Dentist saves prescription", "POST", $"/api/visits/{visitId}/prescription", "DENTIST",
            new { expectedVersion = 0, diagnosis = "Synthetic diagnosis", items = lines, previewToken = preview.GetProperty("token").GetString() }, 201);
        await Api("Dentist completes visit", "PUT", $"/api/visits/{visitId}/complete", "DENTIST");
        var pendingInvoice = await Api("Reception reads payment due", "GET", $"/api/invoices/by-visit/{visitId}", "RECEPTIONIST");
        check(pendingInvoice.GetProperty("status").GetInt32() == 1 && Money(pendingInvoice.GetProperty("totalAmount")) == 202000, "Completed visit requires service plus medicine payment");
        await Api("Clinical write blocked after completion", "POST", $"/api/visits/{visitId}/tooth-conditions", "DENTIST", new { toothNumber = 12, surface = "B", conditionCode = "CARIES" }, 409);
        var payment = new { amount = 202000, amountTendered = 210000, requestId = Guid.NewGuid() };
        await Api("Assistant cannot collect payment", "POST", $"/api/invoices/{invoiceId}/payments", "ASSISTANT", payment, 403);
        var paid = await Api("Reception collects cash", "POST", $"/api/invoices/{invoiceId}/payments", "RECEPTIONIST", payment);
        var replayPayment = await Api("Cash request is idempotent", "POST", $"/api/invoices/{invoiceId}/payments", "RECEPTIONIST", payment);
        check(paid.GetProperty("id").GetInt32() == replayPayment.GetProperty("id").GetInt32(), "No duplicate receipt on replay");
        check(await db.PaymentTransactions.CountAsync(x => x.InvoiceId == invoiceId) == 1, "Exactly one persisted payment");
        await Api("Patient reads own clinical timeline", "GET", $"/api/patients/{patientId}/timeline", "PATIENT");
        await Api("Patient reads own prescription", "GET", $"/api/visits/{visitId}/prescription", "PATIENT");
        await Api("Patient cannot edit prescription", "PUT", $"/api/visits/{visitId}/prescription", "PATIENT", new { }, 403);
        await Api("Admin reads audit", "GET", "/api/audit-logs", "ADMIN");
        await Api("Staff can be locked", "POST", "/api/staff/" + users["ASSISTANT"] + "/lock", "ADMIN");
        await Api("Locked staff session rejected", "GET", "/api/auth/me", "ASSISTANT", expected: 401);
        await Api("Staff can be unlocked", "POST", "/api/staff/" + users["ASSISTANT"] + "/unlock", "ADMIN");
        await Login("ASSISTANT", "0999999004", host.Password);
        await Api("Patient logout", "POST", "/api/auth/logout", "PATIENT");
        check(users.Keys.Count == 5, "Five roles exercised through the owned API");
    }
}
