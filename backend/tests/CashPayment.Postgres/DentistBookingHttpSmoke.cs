using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Appointments.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

internal static class DentistBookingHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var role = await db.Roles.SingleAsync(r => r.RoleCode == RoleCodes.Dentist);
        User Dentist(string name, string phone, bool active = true) => new()
        { RoleId = role.RoleId, FullName = name, Phone = phone, IsActive = active, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
        var dentist = Dentist("Synthetic counter dentist", "0999999951");
        var secondDentist = Dentist("Synthetic second counter dentist", "0999999952");
        var inactive = Dentist("Synthetic inactive dentist", "0999999953", false);
        db.Users.AddRange(dentist, secondDentist, inactive);
        var patients = Enumerable.Range(0, 12).Select(n => new Patient { FullName = $"Synthetic booking patient {n}", Phone = "0999999954", DateOfBirth = new(1990, 1, 1), UserId = actorId }).ToArray();
        db.Patients.AddRange(patients); await db.SaveChangesAsync();
        var clock = new VietnamClock(SmokeTestTimeProvider.Instance); var target = clock.Today.AddDays(120); var time = new TimeOnly(9, 0);
        var checkedIn = new Appointment { PatientId = patients[5].PatientId, DentistId = dentist.UserId, AppointmentDate = target.AddDays(4), SlotTime = time, Status = AppointmentStatuses.CheckedIn, CreatedByUserId = actorId };
        var toMove = new Appointment { PatientId = patients[8].PatientId, DentistId = dentist.UserId, AppointmentDate = target.AddDays(5), SlotTime = time, Status = AppointmentStatuses.Scheduled, CreatedByUserId = actorId };
        var toRollback = new Appointment { PatientId = patients[11].PatientId, DentistId = secondDentist.UserId, AppointmentDate = target.AddDays(6), SlotTime = time, Status = AppointmentStatuses.Scheduled, CreatedByUserId = actorId };
        db.Appointments.AddRange(checkedIn, toMove, toRollback); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var request = new CreateAppointmentRequest(patients[0].PatientId, target, time, dentist.UserId, "Synthetic visit reason");
        const string url = "/api/appointments/booking-dentists";
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync(url)) check(response.StatusCode == HttpStatusCode.Unauthorized, "Booking dentist picker anonymous 401");
        foreach (var denied in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        { authenticate(denied); using var response = await client.GetAsync(url); check(response.StatusCode == HttpStatusCode.Forbidden, $"Booking dentist picker {denied} 403"); }
        foreach (var allowed in new[] { RoleCodes.Admin, RoleCodes.Receptionist })
        {
            authenticate(allowed); using var response = await client.GetAsync(url);
            var result = await response.Content.ReadFromJsonAsync<BookingDentistResponse[]>();
            check(response.StatusCode == HttpStatusCode.OK && result is not null && result.Any(d => d.DentistId == dentist.UserId) && result.Any(d => d.DentistId == secondDentist.UserId) && result.All(d => d.DentistId != inactive.UserId && d.DentistId != actorId), $"Booking dentist picker {allowed} sees only active dentists");
            check(response.Headers.CacheControl?.NoStore == true, "Booking dentist picker no-store");
        }
        using (var response = await client.GetAsync(url))
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(json.RootElement.EnumerateArray().All(d => d.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "dentistId", "dentistName" })), "Booking dentist picker exposes only IDs and names");
        }
        using (var response = await client.PostAsJsonAsync("/api/appointments", request))
        {
            var result = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
            check(response.StatusCode == HttpStatusCode.Created && result?.DentistId == dentist.UserId && result.DentistName == dentist.FullName, "Counter booking selected dentist returned from stored appointment");
            var audit = await db.AuditLogs.SingleAsync(a => a.EntityId == result!.AppointmentId && a.Action == AuditActions.AppointmentCreated);
            check(audit.UserId == actorId && audit.Detail!.Contains("DentistId") && !audit.Detail.Contains(request.Notes!), "Counter booking actor/assignment audit has no medical text");
        }
        var conflict = request with { PatientId = patients[1].PatientId };
        foreach (var actorRole in new[] { RoleCodes.Receptionist, RoleCodes.Patient })
        {
            authenticate(actorRole); using var response = await client.PostAsJsonAsync("/api/appointments", conflict);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            check(response.StatusCode == HttpStatusCode.BadRequest && json.RootElement.GetProperty("message").GetString()!.Contains("Bác sĩ đã có lịch"), $"Booking dentist conflict {actorRole} 400");
        }
        authenticate(RoleCodes.Receptionist);
        foreach (var bad in new[] { conflict with { DentistId = 0 }, conflict with { DentistId = -1 }, conflict with { DentistId = int.MaxValue }, conflict with { DentistId = inactive.UserId }, conflict with { DentistId = actorId }, conflict with { SlotTime = new(9, 5) } })
        { using var response = await client.PostAsJsonAsync("/api/appointments", bad); check(response.StatusCode == HttpStatusCode.BadRequest, "Booking rejects invalid/inactive/non-dentist/time 400"); }
        using (var response = await client.PostAsJsonAsync("/api/appointments", conflict with { DentistId = secondDentist.UserId })) check(response.StatusCode == HttpStatusCode.Created, "Different dentist same slot allowed");
        using (var response = await client.PostAsJsonAsync("/api/appointments", request with { PatientId = patients[2].PatientId, SlotTime = new(9, 30) })) check(response.StatusCode == HttpStatusCode.Created, "Same dentist adjacent slot allowed");
        using (var response = await client.PostAsJsonAsync("/api/appointments", request with { PatientId = patients[3].PatientId, DentistId = null })) check(response.StatusCode == HttpStatusCode.Created, "Unassigned booking remains supported");
        using (var response = await client.PostAsJsonAsync("/api/appointments", request with { PatientId = patients[4].PatientId, AppointmentDate = checkedIn.AppointmentDate })) check(response.StatusCode == HttpStatusCode.Created, "Checked-in appointment no longer reserves future scheduled slot");
        check(await db.Appointments.CountAsync(a => a.AppointmentDate == target && a.DentistId == dentist.UserId && a.SlotTime == time) == 1, "Rejected duplicate requests leave one scheduled dentist booking");

        var emergencyDay = target.AddDays(20);
        var conflictingBooking = request with { PatientId = patients[5].PatientId, AppointmentDate = emergencyDay };
        authenticate(RoleCodes.Receptionist);
        int conflictAppointmentId;
        using (var response = await client.PostAsJsonAsync("/api/appointments", conflictingBooking))
        {
            var booking = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
            check(response.StatusCode == HttpStatusCode.Created && booking is not null, "Create standard appointment for emergency override test");
            conflictAppointmentId = booking!.AppointmentId;
        }
        var emergencyRequest = conflictingBooking with
        {
            PatientId = patients[6].PatientId,
            SlotTime = time,
            IsEmergency = true,
            EmergencyReason = "Synthetic emergency reason"
        };
        using (var response = await client.PostAsJsonAsync("/api/appointments", emergencyRequest))
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var details = json.RootElement.GetProperty("details");
            check(response.StatusCode == HttpStatusCode.Conflict && json.RootElement.GetProperty("code").GetString() == "APP_011" &&
                details.GetProperty("conflictAppointmentId").GetInt32() == conflictAppointmentId &&
                details.GetProperty("dentistName").GetString() == dentist.FullName &&
                details.GetProperty("patientName").GetString() == patients[5].FullName &&
                json.RootElement.GetProperty("message").GetString()!.Contains("09:00"), "Emergency conflict returns dentist, booked patient, time and exact appointment confirmation");
        }
        using (var response = await client.PostAsJsonAsync("/api/appointments", emergencyRequest with { ConfirmedDentistConflictAppointmentId = conflictAppointmentId }))
        {
            var booking = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
            check(response.StatusCode == HttpStatusCode.Created && booking?.IsEmergency == true && booking.EmergencyReason == emergencyRequest.EmergencyReason,
                "Emergency dentist override succeeds only after matching conflict confirmation");
            var audit = await db.AuditLogs.SingleAsync(a => a.EntityId == booking!.AppointmentId && a.Action == AuditActions.AppointmentCreated);
            check(audit.Detail!.Contains("confirmedDentistConflictOverride\":true") && !audit.Detail.Contains(emergencyRequest.EmergencyReason!),
                "Emergency override audit records confirmation without logging reason text");
        }
        authenticate(RoleCodes.Patient);
        using (var response = await client.PostAsJsonAsync("/api/appointments", emergencyRequest with { PatientId = patients[7].PatientId, AppointmentDate = emergencyDay.AddDays(1), DentistId = null }))
            check(response.StatusCode == HttpStatusCode.Forbidden, "Patient cannot create emergency booking");
        authenticate(RoleCodes.Receptionist);
        using (var response = await client.PostAsJsonAsync("/api/appointments", emergencyRequest with
        {
            PatientId = patients[8].PatientId,
            AppointmentDate = emergencyDay.AddDays(2),
            SlotTime = new(18, 10),
            DentistId = null
        }))
        {
            var booking = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
            check(response.StatusCode == HttpStatusCode.Created && booking?.SlotTime == new TimeOnly(18, 10), "Emergency booking accepts after-hours free time");
        }

        var queuePatients = new[]
        {
            new Patient { FullName = "Synthetic regular queue patient", Phone = "0999999954", DateOfBirth = new(1990, 1, 1), UserId = actorId },
            new Patient { FullName = "Synthetic emergency queue patient", Phone = "0999999954", DateOfBirth = new(1990, 1, 1), UserId = actorId }
        };
        db.Patients.AddRange(queuePatients);
        await db.SaveChangesAsync();
        var queueNormal = new Appointment
        {
            PatientId = queuePatients[0].PatientId,
            DentistId = dentist.UserId,
            AppointmentDate = clock.Today,
            SlotTime = new(8, 0),
            Status = AppointmentStatuses.Scheduled,
            CreatedByUserId = actorId
        };
        var queueEmergency = new Appointment
        {
            PatientId = queuePatients[1].PatientId,
            DentistId = dentist.UserId,
            AppointmentDate = clock.Today,
            SlotTime = new(18, 0),
            Status = AppointmentStatuses.Scheduled,
            IsEmergency = true,
            EmergencyReason = "Synthetic emergency reason",
            CreatedByUserId = actorId
        };
        db.Appointments.AddRange(queueNormal, queueEmergency);
        await db.SaveChangesAsync();
        db.QueueEntries.AddRange(
            new QueueEntry { PatientId = queueNormal.PatientId, AppointmentId = queueNormal.AppointmentId, QueueDate = clock.Today, QueueNumber = 990001, IsPriority = true, CheckInTime = DateTime.UtcNow.AddMinutes(-2), Status = QueueStatus.Waiting, CreatedAt = DateTime.UtcNow },
            new QueueEntry { PatientId = queueEmergency.PatientId, AppointmentId = queueEmergency.AppointmentId, QueueDate = clock.Today, QueueNumber = 990002, IsPriority = true, CheckInTime = DateTime.UtcNow.AddMinutes(-1), Status = QueueStatus.Waiting, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var queueRows = await new QueueRepository(db).GetTodayQueueAsync(clock.Today, QueueStatus.Waiting, null, 1, 100);
        var queueAppointmentIds = queueRows.Items.Where(q => q.AppointmentId == queueNormal.AppointmentId || q.AppointmentId == queueEmergency.AppointmentId)
            .Select(q => q.AppointmentId).ToList();
        check(queueAppointmentIds.IndexOf(queueEmergency.AppointmentId) >= 0 &&
            queueAppointmentIds.IndexOf(queueEmergency.AppointmentId) < queueAppointmentIds.IndexOf(queueNormal.AppointmentId),
            "Emergency check-in sorts ahead of regular appointment-priority queue entries");

        AppointmentService Service(DentalDbContext context, IAuditLogger? logger = null) => new(new AppointmentRepository(context), new PatientRepository(context), new UserRepository(context), new UnitOfWork(context), logger ?? new AuditLogger(context, NullLogger<AuditLogger>.Instance), clock);
        async Task<Result<AppointmentResponse>> CreateRace(DbContextOptions<DentalDbContext> raceOptions, int patientIndex, DateOnly day)
        {
            await using var context = new DentalDbContext(raceOptions);
            return await Service(context).CreateAppointmentAsync(actorId, RoleCodes.Receptionist, request with { PatientId = patients[patientIndex].PatientId, AppointmentDate = day }, null);
        }
        var createRaceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        var createResults = await Task.WhenAll(CreateRace(createRaceOptions, 6, target.AddDays(10)), CreateRace(createRaceOptions, 7, target.AddDays(10)));
        check(createResults.Count(r => r.IsSuccess) == 1 && createResults.Count(r => r.Error.Code == "APP_009") == 1, "Concurrent creates same dentist slot one success/one409 conflict");
        check(await db.Appointments.CountAsync(a => a.AppointmentDate == target.AddDays(10) && a.DentistId == dentist.UserId && a.SlotTime == time) == 1, "Concurrent creates never double-book dentist");
        var createWinner = createResults.Single(r => r.IsSuccess).Value!;
        check(await db.AuditLogs.CountAsync(a => a.Action == AuditActions.AppointmentCreated && a.EntityId == createWinner.AppointmentId) == 1, "Concurrent create winner has one audit");

        var mixedOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        async Task<Result<AppointmentResponse>> MoveRace()
        {
            await using var context = new DentalDbContext(mixedOptions);
            return await Service(context).RescheduleAppointmentAsync(toMove.AppointmentId, actorId, RoleCodes.Receptionist, new(target.AddDays(11), time, Reason: "Synthetic concurrent move"), null);
        }
        var mixed = await Task.WhenAll(CreateRace(mixedOptions, 9, target.AddDays(11)), MoveRace());
        check(mixed.Count(r => r.IsSuccess) == 1 && mixed.Count(r => r.Error.Code == "APP_009") == 1, "Create versus reschedule same dentist slot one success/one409 conflict");
        check(await db.Appointments.CountAsync(a => a.AppointmentDate == target.AddDays(11) && a.DentistId == dentist.UserId && a.SlotTime == time) == 1, "Create versus reschedule keeps one slot booking");
        var moved = await db.Appointments.AsNoTracking().SingleAsync(a => a.AppointmentId == toMove.AppointmentId);
        check(moved.RescheduleCount == (mixed[1].IsSuccess ? 1 : 0) && moved.AppointmentDate == (mixed[1].IsSuccess ? target.AddDays(11) : target.AddDays(5)), "Create versus reschedule loser rolls back date/count");

        await using (var failingDb = new DentalDbContext(options))
        {
            var caught = false;
            try { await Service(failingDb, new ThrowAudit()).CreateAppointmentAsync(actorId, RoleCodes.Admin, request with { PatientId = patients[10].PatientId, AppointmentDate = target.AddDays(12) }, null); }
            catch (InvalidOperationException) { caught = true; }
            check(caught && !await db.Appointments.AnyAsync(a => a.PatientId == patients[10].PatientId), "Thrown creation audit failure rolls back new appointment");
        }
        // Freeing a slot by rescheduling permits a later normal create into the old slot.
        authenticate(RoleCodes.Admin);
        using (var response = await client.PutAsJsonAsync($"/api/appointments/{toRollback.AppointmentId}/reschedule", new RescheduleAppointmentRequest(target.AddDays(13), time, Reason: "Synthetic release"))) check(response.StatusCode == HttpStatusCode.OK, "Existing reschedule releases original dentist slot");
        using (var response = await client.PostAsJsonAsync("/api/appointments", request with { PatientId = patients[10].PatientId, DentistId = secondDentist.UserId, AppointmentDate = target.AddDays(6) })) check(response.StatusCode == HttpStatusCode.Created, "Create can reuse released dentist slot");
    }
    private sealed class Rendezvous : SaveChangesInterceptor
    {
        private int arrivals;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken); return result;
        }
    }
    private sealed class ThrowAudit : IAuditLogger
    {
        public Task LogAsync(string action, string entityType, int? entityId = null, int? userId = null, string? detail = null, string? ipAddress = null, CancellationToken ct = default)
            => throw new InvalidOperationException("Synthetic creation audit failure");
    }
}
