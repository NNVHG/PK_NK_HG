using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Appointments.Services;
using Dental.Application.Features.Appointments.Validators;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

internal static class SchedulingSettingsHttpSmoke
{
    // EF model of only the pre-migration columns, used to exercise upgrade of an existing row.
    private sealed class LegacyBooking
    {
        public int AppointmentId { get; set; } public int PatientId { get; set; } public DateOnly AppointmentDate { get; set; }
        public TimeOnly SlotTime { get; set; } public int? DentistId { get; set; } public string Status { get; set; } = AppointmentStatuses.Scheduled;
        public int CreatedByUserId { get; set; } public DateTime CreatedAt { get; set; }
    }
    private sealed class LegacyConfig { public string Key { get; set; } = ""; public string Value { get; set; } = ""; }
    private sealed class LegacyContext(DbContextOptions<LegacyContext> options) : DbContext(options)
    {
        public DbSet<LegacyBooking> Bookings => Set<LegacyBooking>();
        public DbSet<LegacyConfig> Configs => Set<LegacyConfig>();
        protected override void OnModelCreating(ModelBuilder b) { b.Entity<LegacyBooking>().ToTable("Appointments").HasKey(x => x.AppointmentId); b.Entity<LegacyConfig>().ToTable("SystemConfigs").HasKey(x => x.Key); }
    }
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Fail && data.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action == "MOD_MST_SCHEDULE_UPDATED")) throw new InvalidOperationException("Synthetic audit failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool, string> check)
    {
        var failure = new FailAudit(); var configured = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure).Options;
        await using var db = new DentalDbContext(configured); var users = new Dictionary<string, User>();
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Receptionist, RoleCodes.Patient })
        { var u = new User { Role = new Role { RoleCode = role, RoleName = "Schedule " + role }, FullName = "Synthetic schedule actor " + role, Phone = "093000000" + users.Count, PasswordHash = "synthetic-not-used" }; users[role] = u; db.Users.Add(u); }
        await db.SaveChangesAsync();
        var patient = new Patient { FullName = "Synthetic schedule patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0930000008", UserId = users[RoleCodes.Patient].UserId }; db.Patients.Add(patient); await db.SaveChangesAsync();
        var day = new VietnamClock(TimeProvider.System).Today.AddDays(5);
        int legacyId;
        await using (var legacy = new LegacyContext(new DbContextOptionsBuilder<LegacyContext>().UseNpgsql(db.Database.GetConnectionString()).Options))
        { var row = new LegacyBooking { PatientId = patient.PatientId, AppointmentDate = day, SlotTime = new(8, 30), DentistId = users[RoleCodes.Dentist].UserId, CreatedByUserId = users[RoleCodes.Admin].UserId, CreatedAt = DateTime.UtcNow }; legacy.Bookings.Add(row); legacy.Configs.Add(new LegacyConfig { Key = "Clinic:Address", Value = "Synthetic clinic location" }); await legacy.SaveChangesAsync(); legacyId = row.AppointmentId; }
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        var upgraded = await db.Appointments.AsNoTracking().SingleAsync(x => x.AppointmentId == legacyId);
        check(upgraded.SlotTime == new TimeOnly(8, 30) && upgraded.SlotDurationMinutes == 30 && upgraded.AppointmentDate == day, "Migration backfills existing appointment duration30 without moving its time");
        var upgradedConfig = await db.SystemConfigs.AsNoTracking().SingleAsync(x => x.Key == "Clinic:Address");
        check(upgradedConfig.Id > 0 && upgradedConfig.Value == "Synthetic clinic location" && upgradedConfig.UpdatedAt is null && upgradedConfig.UpdatedByUserId is null, "Config PK/column upgrade preserves old value without fabricating updater history");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" }); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); builder.Configuration["Jwt:Secret"] = secret;
        builder.Services.AddScoped(_ => new DentalDbContext(configured)); builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddScoped<ISchedulingSettingsRepository, SchedulingSettingsRepository>(); builder.Services.AddScoped<SchedulingSettingsService>();
        builder.Services.AddScoped<IClinicPrintSettingsRepository, ClinicPrintSettingsRepository>(); builder.Services.AddScoped<ClinicPrintSettingsService>();
        builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>(); builder.Services.AddScoped<IPatientRepository, PatientRepository>(); builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>(); builder.Services.AddScoped<IAuditLogger, AuditLogger>(); builder.Services.AddSingleton(new VietnamClock(TimeProvider.System)); builder.Services.AddScoped<AppointmentService>();
        builder.Services.AddScoped<CreateAppointmentRequestValidator>(); builder.Services.AddScoped<AppointmentQueryRequestValidator>(); builder.Services.AddScoped<RescheduleAppointmentRequestValidator>(); builder.Services.AddScoped<CancelAppointmentRequestValidator>(); builder.Services.AddScoped<SelfCancellationEligibilityRequestValidator>();
        builder.Services.AddControllers().AddApplicationPart(typeof(SchedulingSettingsController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt => { jwt.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "DentalClinic", ValidateAudience = true, ValidAudience = "DentalClinicClient", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), ClockSkew = TimeSpan.Zero }; jwt.Events = new JwtBearerEvents { OnTokenValidated = AccessTokenVersionValidation.ValidateAsync }; });
        builder.Services.AddAuthorization(x => x.AddApplicationPolicies());
        await using var app = builder.Build(); app.Use(async (c, next) => { try { await next(c); } catch { c.Response.StatusCode = 500; } }); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) }; var tokens = new JwtTokenService(builder.Configuration);
        void As(string? role) => client.DefaultRequestHeaders.Authorization = role is null ? null : new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(users[role]));
        SchedulingSettingsRequest Request(SchedulingSettingsDto d, int minutes = 60) => new("08:00", "12:00", "13:00", "18:00", minutes, d.Revision);
        async Task<SchedulingSettingsDto> Read() => (await client.GetFromJsonAsync<SchedulingSettingsDto>("/api/master/configs"))!;
        async Task<Patient> Patient()
        { var p = new Patient { FullName = "Synthetic additional schedule patient", DateOfBirth = new(1990, 1, 1), Phone = "0930000099" }; db.Patients.Add(p); await db.SaveChangesAsync(); return p; }
        async Task<HttpResponseMessage> Book(int owner, DateOnly date, TimeOnly time, string? revision = null, int? dentist = null) => await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(owner, date, time, dentist, ExpectedScheduleRevision: revision));
        try
        {
            As(null); using (var r = await client.GetAsync("/api/master/configs")) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous cannot read configs");
            using (var r = await client.GetAsync("/api/appointments/booking-schedule")) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous cannot read booking schedule");
            foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Receptionist }) { As(role); using var r = await client.GetAsync("/api/master/configs"); check(r.StatusCode == HttpStatusCode.Forbidden, role + " raw config read denied"); using var w = await client.PutAsJsonAsync("/api/master/configs", new SchedulingSettingsRequest("08:00", "12:00", "13:00", "18:00", 60, new string('A', 64))); check(w.StatusCode == HttpStatusCode.Forbidden, role + " config write denied"); }
            foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Receptionist, RoleCodes.Admin }) { As(role); using var r = await client.GetAsync("/api/appointments/booking-schedule"); var result = await r.Content.ReadFromJsonAsync<BookingScheduleDto>(); check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true && result!.Slots.Length == 21 && result.SlotDurationMinutes == 30, role + " reads derived DL174 fallback slots NoStore"); }
            foreach (var role in new[] { RoleCodes.Dentist, RoleCodes.Assistant }) { As(role); using var r = await client.GetAsync("/api/appointments/booking-schedule"); check(r.StatusCode == HttpStatusCode.Forbidden, role + " has no booking selector permission"); }
            As(RoleCodes.Admin); using (var r = await client.GetAsync("/api/master/configs")) check(r.Headers.CacheControl?.NoStore == true, "Raw admin config NoStore");
            var initial = await Read(); check(initial.UsingFallback && initial.WorkEndTime == "20:00" && initial.LunchStartTime == "12:00", "Unset five keys use safe DL174 defaults");
            using (var r = await client.PutAsJsonAsync("/api/master/configs", Request(initial))) check(r.StatusCode == HttpStatusCode.OK, "Admin saves five schedule keys");
            var current = await Read(); check(!current.UsingFallback && current.SlotDurationMinutes == 60 && current.Revision != initial.Revision, "Saved schedule has new revision and duration");
            check(await db.SystemConfigs.CountAsync(x => ClinicScheduleReader.Keys.Contains(x.Key)) == 5 && await db.AuditLogs.CountAsync(x => x.Action == "MOD_MST_SCHEDULE_UPDATED") == 1, "Five config keys and one required audit commit together");
            check(await db.SystemConfigs.CountAsync(x => ClinicScheduleReader.Keys.Contains(x.Key) && x.UpdatedByUserId == users[RoleCodes.Admin].UserId && x.UpdatedAt != null) == 5 && await db.SystemConfigs.AnyAsync(x => x.Key == "Appointment:DefaultSlotDurationMinutes" && x.DataType == "Number"), "Config metadata records updater, time and datatype for all five keys");
            upgraded = await db.Appointments.AsNoTracking().SingleAsync(x => x.AppointmentId == legacyId); check(upgraded.SlotTime == new TimeOnly(8, 30) && upgraded.SlotDurationMinutes == 30 && upgraded.Status == AppointmentStatuses.Scheduled, "Updating config does not cancel, move or resize old appointment");
            using (var r = await client.PutAsJsonAsync("/api/master/configs", Request(initial, 45))) check(r.StatusCode == HttpStatusCode.Conflict, "Stale admin revision rejected");
            foreach (var bad in new[] { Request(current) with { SlotDurationMinutes = 0 }, Request(current) with { WorkStartTime = "8:00" }, Request(current) with { LunchEndTime = "11:00" }, Request(current) with { ExpectedRevision = "" }, Request(current) with { SlotDurationMinutes = 1440 } }) { using var r = await client.PutAsJsonAsync("/api/master/configs", bad); check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid schedule or revision rejected"); }
            using (var r = await client.PutAsJsonAsync("/api/master/configs", new { current.WorkStartTime, current.LunchStartTime, current.LunchEndTime, current.WorkEndTime, slotDurationMinutes = 0.5m, expectedRevision = current.Revision })) check(r.StatusCode == HttpStatusCode.BadRequest, "Fractional duration JSON rejected");
            failure.Fail = true; using (var r = await client.PutAsJsonAsync("/api/master/configs", Request(current, 45))) check(r.StatusCode == HttpStatusCode.InternalServerError, "Audit failure rejects schedule update"); failure.Fail = false;
            check((await Read()).Revision == current.Revision && await db.AuditLogs.CountAsync(x => x.Action == "MOD_MST_SCHEDULE_UPDATED") == 1, "Failed audit rolls back all config keys");
            var newPatient = await Patient();
            using (var r = await Book(newPatient.PatientId, day.AddDays(1), new(8, 30), current.Revision)) check(r.StatusCode == HttpStatusCode.BadRequest, "New booking cannot use removed half-hour grid");
            using (var r = await Book(newPatient.PatientId, day, new(8, 0), current.Revision, users[RoleCodes.Dentist].UserId)) check(r.StatusCode == HttpStatusCode.BadRequest, "Old0830 duration30 blocks new0800 duration60 for same dentist");
            using (var r = await Book(newPatient.PatientId, day, new(9, 0), current.Revision, users[RoleCodes.Dentist].UserId)) { var response = await r.Content.ReadFromJsonAsync<AppointmentResponse>(); check(r.StatusCode == HttpStatusCode.Created && response!.SlotDurationMinutes == 60, "Adjacent0900 booking succeeds and snapshots60"); }
            var nextPatient = await Patient(); using (var r = await Book(nextPatient.PatientId, day.AddDays(2), new(8, 0), initial.Revision)) check(r.StatusCode == HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("APP_SCHEDULE"), "Stale booking selector revision rejected before save");
            using (var r = await client.PutAsJsonAsync($"/api/appointments/{legacyId}/reschedule", new RescheduleAppointmentRequest(day, new(8, 30), Reason: "Keep existing appointment", ExpectedScheduleRevision: current.Revision))) { var response = await r.Content.ReadFromJsonAsync<AppointmentResponse>(); check(r.StatusCode == HttpStatusCode.OK && response!.SlotDurationMinutes == 30 && response.SlotTime == new TimeOnly(8, 30), "No-op reschedule preserves old grid and duration"); }
            using (var r = await client.PutAsJsonAsync($"/api/appointments/{legacyId}/reschedule", new RescheduleAppointmentRequest(day.AddDays(1), new(10, 0), Reason: "Synthetic reschedule", ExpectedScheduleRevision: current.Revision))) { var response = await r.Content.ReadFromJsonAsync<AppointmentResponse>(); check(r.StatusCode == HttpStatusCode.OK && response!.SlotDurationMinutes == 60 && response.RescheduleCount == 1, "Actual reschedule uses current duration60 and retains identity"); }
            // Sweep-line capacity must preserve both old groups, without treating disjoint halves as120 concurrent people.
            var capacityDay = day.AddDays(3);
            db.Appointments.AddRange(Enumerable.Range(0, 60).Select(_ => new Appointment { PatientId = patient.PatientId, AppointmentDate = capacityDay, SlotTime = new(8, 0), SlotDurationMinutes = 30, CreatedByUserId = users[RoleCodes.Admin].UserId }));
            db.Appointments.AddRange(Enumerable.Range(0, 60).Select(_ => new Appointment { PatientId = patient.PatientId, AppointmentDate = capacityDay, SlotTime = new(8, 30), SlotDurationMinutes = 30, CreatedByUserId = users[RoleCodes.Admin].UserId })); await db.SaveChangesAsync();
            using (var r = await Book(nextPatient.PatientId, capacityDay, new(8, 0), current.Revision)) check(r.StatusCode == HttpStatusCode.Created, "Two disjoint old half-hour groups60 each do not falsely exhaust60minute slot");
            db.Appointments.AddRange(Enumerable.Range(0, 39).Select(_ => new Appointment { PatientId = patient.PatientId, AppointmentDate = capacityDay, SlotTime = new(8, 0), SlotDurationMinutes = 60, CreatedByUserId = users[RoleCodes.Admin].UserId })); await db.SaveChangesAsync();
            var fullPatient = await Patient(); using (var r = await Book(fullPatient.PatientId, capacityDay, new(8, 0), current.Revision)) check(r.StatusCode == HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("APP_002"), "Peak100 old/new overlapping bookings block another reservation");
            var raceDay = day.AddDays(4); var first = await Patient(); var second = await Patient();
            var race = await Task.WhenAll(Book(first.PatientId, raceDay, new(8, 0), current.Revision, users[RoleCodes.Dentist].UserId), Book(second.PatientId, raceDay, new(8, 0), current.Revision, users[RoleCodes.Dentist].UserId));
            check(race.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && race.All(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict or HttpStatusCode.BadRequest), "Concurrent normal bookings for one dentist have one winner"); foreach (var r in race) r.Dispose();
            var updateRace = await Task.WhenAll(client.PutAsJsonAsync("/api/master/configs", Request(current, 45)), client.PutAsJsonAsync("/api/master/configs", Request(current, 20)));
            check(updateRace.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && updateRace.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "Concurrent admin config updates cannot overwrite each other"); foreach (var r in updateRace) r.Dispose();
            var activeSchedule = await Read(); var concurrentPatient = await Patient();
            var saveDuringBooking = client.PutAsJsonAsync("/api/master/configs", Request(activeSchedule, 15));
            var bookDuringSave = Book(concurrentPatient.PatientId, day.AddDays(7), new(8, 0), activeSchedule.Revision);
            await Task.WhenAll(saveDuringBooking, bookDuringSave);
            using (var r = await saveDuringBooking) check(r.StatusCode == HttpStatusCode.OK, "Config can change concurrently without rewriting existing reservations");
            using (var r = await bookDuringSave)
            {
                var rows = await db.Appointments.AsNoTracking().Where(x => x.PatientId == concurrentPatient.PatientId).ToArrayAsync();
                check(r.StatusCode == HttpStatusCode.Created && rows.Single().SlotDurationMinutes == activeSchedule.SlotDurationMinutes ||
                    r.StatusCode == HttpStatusCode.Conflict && rows.Length == 0, "Booking/config race either preserves selected duration or rejects stale revision");
            }
            var shorter = await Read(); var shorterPatient = await Patient();
            using (var r = await Book(shorterPatient.PatientId, day.AddDays(1), new(10, 30), shorter.Revision, users[RoleCodes.Dentist].UserId)) check(r.StatusCode == HttpStatusCode.BadRequest, "Old60minute reservation blocks a new15minute booking inside it");
            using (var r = await Book(shorterPatient.PatientId, day.AddDays(1), new(11, 0), shorter.Revision, users[RoleCodes.Dentist].UserId)) { var row = await r.Content.ReadFromJsonAsync<AppointmentResponse>(); check(r.StatusCode == HttpStatusCode.Created && row!.SlotDurationMinutes == 15, "New short booking may start exactly at old reservation end"); }
            var corrupt = await db.SystemConfigs.SingleAsync(x => x.Key == "Clinic:WorkStartTime"); corrupt.Value = "invalid"; await db.SaveChangesAsync();
            check((await Read()).UsingFallback && (await Read()).SlotDurationMinutes == 30, "Malformed stored schedule falls back safely as a whole");
            var recovered = await Read(); using (var r = await client.PutAsJsonAsync("/api/master/configs", Request(recovered))) check(r.StatusCode == HttpStatusCode.OK, "Admin repairs malformed schedule using raw revision");
            db.SystemConfigs.Add(new SystemConfig { Key = "Synthetic:PrivateSetting", Value = "synthetic-private-marker" }); await db.SaveChangesAsync();
            using (var r = await client.GetAsync("/api/master/configs")) check(!(await r.Content.ReadAsStringAsync()).Contains("synthetic-private-marker"), "Typed config endpoint never returns unrelated private settings");
            As(RoleCodes.Patient); using (var r = await client.GetAsync("/api/appointments/booking-schedule")) check(!(await r.Content.ReadAsStringAsync()).Contains("synthetic-private-marker"), "Derived booking schedule never exposes private settings");
            var effective = (await client.GetFromJsonAsync<BookingScheduleDto>("/api/appointments/booking-schedule"))!;
            using (var r = await Book(patient.PatientId, day.AddDays(9), new(8, 0), effective.Revision)) check(r.StatusCode == HttpStatusCode.Created, "Patient can book own profile on dynamic schedule");
            var child = new Patient { FullName = "Synthetic family child", DateOfBirth = new(2020, 1, 1), Phone = patient.Phone, UserId = users[RoleCodes.Patient].UserId }; db.Patients.Add(child); await db.SaveChangesAsync();
            using (var r = await Book(child.PatientId, day.AddDays(9), new(9, 0), effective.Revision)) check(r.StatusCode == HttpStatusCode.Created, "Family member booking still uses explicitly selected owned profile");
            using (var r = await Book(newPatient.PatientId, day.AddDays(9), new(10, 0), effective.Revision)) check(r.StatusCode == HttpStatusCode.NotFound, "Dynamic scheduling does not expand patient ownership");
            using (var r = await client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest(patient.PatientId, day.AddDays(10), new(8, 15), IsEmergency: true, EmergencyReason: "Synthetic urgent visit", ExpectedScheduleRevision: effective.Revision))) check(r.StatusCode == HttpStatusCode.Forbidden, "Patient cannot invoke emergency exception");
            var ownAppointment = await db.Appointments.AsNoTracking().SingleAsync(x => x.PatientId == patient.PatientId && x.AppointmentDate == day.AddDays(9));
            using (var r = await client.PutAsJsonAsync($"/api/appointments/{ownAppointment.AppointmentId}/reschedule", new RescheduleAppointmentRequest(day.AddDays(10), new(10, 0), Reason: "Synthetic family change", ExpectedScheduleRevision: effective.Revision))) check(r.StatusCode == HttpStatusCode.OK && (await r.Content.ReadFromJsonAsync<AppointmentResponse>())!.SlotDurationMinutes == effective.SlotDurationMinutes, "Patient reschedules using latest duration without changing ownership");
            As(RoleCodes.Receptionist); var urgentPatient = await Patient();
            var urgent = new CreateAppointmentRequest(urgentPatient.PatientId, raceDay, new(8, 15), users[RoleCodes.Dentist].UserId, IsEmergency: true, EmergencyReason: "Synthetic urgent visit", ExpectedScheduleRevision: effective.Revision);
            int conflictId;
            using (var r = await client.PostAsJsonAsync("/api/appointments", urgent))
            { var body = await r.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); check(r.StatusCode == HttpStatusCode.Conflict && body.GetProperty("code").GetString() == "APP_011", "Emergency interval overlap still requires explicit staff override"); conflictId = body.GetProperty("details").GetProperty("conflictAppointmentId").GetInt32(); }
            using (var r = await client.PostAsJsonAsync("/api/appointments", urgent with { ConfirmedDentistConflictAppointmentId = conflictId })) check(r.StatusCode == HttpStatusCode.Created && (await r.Content.ReadFromJsonAsync<AppointmentResponse>())!.IsEmergency, "Confirmed emergency override retains staff-only existing exception");
            var backwards = await Patient(); using (var r = await Book(backwards.PatientId, day.AddDays(14), new(10, 0))) check(r.StatusCode == HttpStatusCode.Created && (await r.Content.ReadFromJsonAsync<AppointmentResponse>())!.SlotDurationMinutes == effective.SlotDurationMinutes, "Older client without revision still receives current duration snapshot");
            As(RoleCodes.Admin); var print = (await client.GetFromJsonAsync<ClinicPrintSettings>("/api/clinic/print-settings"))!;
            check(print.Address == "Synthetic clinic location", "Existing print settings read works after config key/column migration");
            using (var r = await client.PutAsJsonAsync("/api/clinic/print-settings", new ClinicPrintSettingsRequest("Updated synthetic clinic location", "0901234567", print.Revision))) check(r.StatusCode == HttpStatusCode.OK, "Existing print config update still finds by unique key after primary key migration");
            check(await db.SystemConfigs.CountAsync(x => (x.Key == "Clinic:Address" || x.Key == "Clinic:Phone") && x.UpdatedByUserId == users[RoleCodes.Admin].UserId) == 2, "Print settings updates also record real updater metadata");
            var admin = await db.Users.SingleAsync(x => x.UserId == users[RoleCodes.Admin].UserId); admin.IsActive = false; await db.SaveChangesAsync(); As(RoleCodes.Admin);
            using (var r = await client.GetAsync("/api/master/configs")) check(r.StatusCode == HttpStatusCode.Forbidden, "Inactive admin with stale JWT cannot read configs");
            using (var r = await client.PutAsJsonAsync("/api/master/configs", Request(recovered))) check(r.StatusCode == HttpStatusCode.Forbidden, "Inactive admin with stale JWT cannot save config");
        }
        finally { await app.StopAsync(); }
    }
}
