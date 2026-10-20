using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Api.RateLimiting;
using Dental.Application.Features.MOD_IMG;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
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

internal static class DentalImageHttpSmoke
{
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Fail && data.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action.StartsWith("MOD_IMG"))) throw new InvalidOperationException("Synthetic audit failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool, string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "dental-images-http-" + Guid.NewGuid().ToString("N")); var failure = new FailAudit();
        var dbOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure).Options;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); builder.Configuration["Jwt:Secret"] = secret;
        builder.Services.AddScoped(_ => new DentalDbContext(dbOptions)); builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton<IDentalImageStorage>(new DentalImageStorage(root)); builder.Services.AddScoped<IDentalImageRepository, DentalImageRepository>(); builder.Services.AddScoped<DentalImageService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(DentalImagesController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt => { jwt.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "DentalClinic", ValidateAudience = true, ValidAudience = "DentalClinicClient", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), ClockSkew = TimeSpan.Zero }; jwt.Events = new JwtBearerEvents { OnTokenValidated = AccessTokenVersionValidation.ValidateAsync }; });
        builder.Services.AddAuthorization(x => x.AddApplicationPolicies());
        builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; DentalImageRateLimits.Configure(o); });
        await using var app = builder.Build(); app.Use(async (c, next) => { try { await next(c); } catch { c.Response.StatusCode = 500; } });
        app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var tokens = new JwtTokenService(builder.Configuration); await using var db = new DentalDbContext(dbOptions);
        var users = new Dictionary<string, User>();
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Receptionist, RoleCodes.Patient })
        { var user = new User { Role = new Role { RoleCode = role, RoleName = "Image " + role }, FullName = "Synthetic image account", Phone = "096000000" + users.Count, PasswordHash = "synthetic-not-used" }; users[role] = user; db.Users.Add(user); }
        await db.SaveChangesAsync();
        var patient = new Patient { FullName = "Synthetic image patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0960000008", UserId = users[RoleCodes.Patient].UserId };
        var other = new Patient { FullName = "Synthetic other image patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0960000009" }; db.Patients.AddRange(patient, other); await db.SaveChangesAsync();
        async Task<Visit> Visit(string state = VisitStatuses.InProgress, int? patientId = null, int? dentist = null)
        { var v = new Visit { PatientId = patientId ?? patient.PatientId, Status = state, CreatedByUserId = users[RoleCodes.Admin].UserId, DentistId = dentist ?? users[RoleCodes.Dentist].UserId }; db.Visits.Add(v); await db.SaveChangesAsync(); return v; }
        void As(string? role) => client.DefaultRequestHeaders.Authorization = role is null ? null : new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(users[role]));
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a7KkAAAAASUVORK5CYII=");
        async Task<HttpResponseMessage> Upload(int? visit = null, int? tooth = null, int? owner = null, string name = "phim.png", string type = "image/png", byte[]? data = null, string category = "Intraoral", string? notes = null)
        {
            using var form = new MultipartFormDataContent(); form.Add(new StringContent((owner ?? patient.PatientId).ToString()), "patientId"); form.Add(new StringContent(category), "category");
            if(notes is not null) form.Add(new StringContent(notes), "notes");
            if (visit is int v) form.Add(new StringContent(v.ToString()), "visitId"); if (tooth is int t) form.Add(new StringContent(t.ToString()), "toothNumber");
            var file = new ByteArrayContent(data ?? png); file.Headers.ContentType = new(type); form.Add(file, "file", name); return await client.PostAsync("/api/dental-images/upload", form);
        }
        async Task<DentalImageDto> Uploaded(int? visit = null, int? tooth = null)
        { using var r = await Upload(visit, tooth); check(r.StatusCode == HttpStatusCode.Created, "Image upload 201"); return (await r.Content.ReadFromJsonAsync<DentalImageDto>())!; }
        int FileCount() => Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length : 0;
        DentalImage Dummy(int visit, long bytes = 1) => new() { PatientId = patient.PatientId, VisitId = visit, StoragePath = "synthetic/" + Guid.NewGuid(), OriginalFileName = "synthetic.png", ContentType = "image/png", SizeBytes = bytes, Category = DentalImageCategory.Intraoral, UploadedByUserId = users[RoleCodes.Admin].UserId, CreatedAt = DateTime.UtcNow };
        try
        {
            var visit = await Visit(); As(null); using (var r = await Upload()) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous cannot upload");
            foreach (var role in new[] { RoleCodes.Receptionist, RoleCodes.Patient }) { As(role); using var r = await Upload(); check(r.StatusCode == HttpStatusCode.Forbidden, role + " cannot upload"); }
            As(RoleCodes.Dentist); var image = await Uploaded(visit.VisitId, 46);
            check(image.VisitId == visit.VisitId && image.ToothNumber == 46 && image.Width == 1, "Metadata persisted in response");
            check(FileCount() == 1, "Physical image stored outside web root");
            check(File.ReadAllBytes(Directory.GetFiles(root,"*",SearchOption.AllDirectories).Single()).SequenceEqual(png), "Stored file bytes equal original multipart image");
            check(await db.AuditLogs.AnyAsync(x => x.EntityId == image.Id && x.Action == "MOD_IMG_UPLOADED"), "Upload audit committed");
            using(var r = await client.GetAsync($"/api/dental-images?patientId={patient.PatientId}")) { check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true, "List protected NoStore"); var json = await r.Content.ReadAsStringAsync(); check(!json.Contains("storagePath", StringComparison.OrdinalIgnoreCase), "Storage path not exposed"); }
            foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Patient }) { As(role); using var r = await client.GetAsync($"/api/dental-images?patientId={patient.PatientId}"); check(r.StatusCode == HttpStatusCode.OK, role + " reads permitted metadata"); }
            As(RoleCodes.Patient); using(var r = await client.GetAsync($"/api/dental-images?patientId={other.PatientId}")) check(r.StatusCode == HttpStatusCode.NotFound, "Patient cannot read other ownership");
            As(RoleCodes.Assistant); var assisted = await Uploaded(visit.VisitId); using(var r = await client.GetAsync($"/api/dental-images?patientId={patient.PatientId}")) check(r.StatusCode == HttpStatusCode.Forbidden, "Assistant upload does not grant file/list read");
            using(var r = await client.PostAsJsonAsync($"/api/dental-images/{assisted.Id}/attach", new AttachDentalImageRequest(visit.VisitId, assisted.Version))) check(r.StatusCode == HttpStatusCode.Forbidden, "Assistant cannot edit metadata link");
            As(RoleCodes.Admin); var independent = await Uploaded();
            foreach(var category in Enum.GetNames<DentalImageCategory>())
            { using var r = await Upload(category:category,notes:"Synthetic angle note"); var row=await r.Content.ReadFromJsonAsync<DentalImageDto>(); check(r.StatusCode==HttpStatusCode.Created && row?.Category==category && row.Notes=="Synthetic angle note", "Approved image category and note: "+category); }
            using(var r=await Upload(category:"Other")) check(r.StatusCode==HttpStatusCode.BadRequest,"Obsolete category is not silently remapped");
            using(var r=await Upload(notes:new string('a',501))) check(r.StatusCode==HttpStatusCode.BadRequest,"Image notes over 500 characters rejected");
            using(var r = await client.PostAsJsonAsync($"/api/dental-images/{independent.Id}/attach", new AttachDentalImageRequest(visit.VisitId, independent.Version))) check(r.StatusCode == HttpStatusCode.OK, "Independent image attached to open visit");
            using(var r = await client.PostAsJsonAsync($"/api/dental-images/{independent.Id}/attach", new AttachDentalImageRequest(visit.VisitId, independent.Version))) check(r.StatusCode == HttpStatusCode.Conflict, "Already attached image cannot be moved/reassigned");
            var foreign = await Visit(patientId: other.PatientId); using(var r = await Upload(foreign.VisitId)) check(r.StatusCode == HttpStatusCode.BadRequest, "Visit cannot belong to another patient");
            foreach(var state in new[]{VisitStatuses.Completed, VisitStatuses.Cancelled}) { var closed = await Visit(state); using var r = await Upload(closed.VisitId); check(r.StatusCode == HttpStatusCode.BadRequest, state + " rejects new files"); }
            var locked = await Visit(); locked.IsLocked = true; await db.SaveChangesAsync(); using(var r = await Upload(locked.VisitId)) check(r.StatusCode == HttpStatusCode.BadRequest, "Locked visit rejects new files");
            var late = await Uploaded();
            using(var r = await client.PostAsJsonAsync($"/api/dental-images/{late.Id}/attach", new AttachDentalImageRequest(locked.VisitId, late.Version))) check(r.StatusCode == HttpStatusCode.BadRequest, "Independent image cannot attach to locked case");
            using(var r = await client.PostAsJsonAsync($"/api/dental-images/{late.Id}/attach", new AttachDentalImageRequest(foreign.VisitId, late.Version))) check(r.StatusCode == HttpStatusCode.BadRequest, "Attach cannot cross patient ownership");
            var created = await Visit(VisitStatuses.Created); await Uploaded(created.VisitId);
            var correcting = await Visit(VisitStatuses.Correcting); await Uploaded(correcting.VisitId);
            As(RoleCodes.Assistant); using(var r = await Upload(correcting.VisitId)) check(r.StatusCode == HttpStatusCode.BadRequest, "Assistant does not gain correction rights");
            As(RoleCodes.Admin);
            foreach(var status in new[]{InvoiceStatus.Paid, InvoiceStatus.PartiallyPaid, InvoiceStatus.PartiallyRefunded, InvoiceStatus.Refunded})
            {
                var paid = await Visit(); db.Invoices.Add(new Invoice { VisitId = paid.VisitId, InvoiceCode = "IMG-" + Guid.NewGuid().ToString("N"), CreatedByUserId = users[RoleCodes.Admin].UserId, Status = status, CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync();
                using var r = await Upload(paid.VisitId); check(r.StatusCode == HttpStatusCode.BadRequest, status + " rejects attachments even with zero recorded balance");
            }
            using(var r = await Upload(tooth: 19)) check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid FDI blocked");
            using(var r = await Upload(name: "fake.jpg")) check(r.StatusCode == HttpStatusCode.BadRequest, "MIME extension mismatch blocked");
            using(var r = await Upload(data: new byte[100])) check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid magic blocked");
            using(var r = await Upload(data: png[..20])) check(r.StatusCode == HttpStatusCode.BadRequest, "Truncated image blocked");
            using(var r = await Upload(data: new byte[10 * 1024 * 1024 + 1])) check(r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge, "Multipart size bound enforced");
            var toothVisit = await Visit(); for(var i = 0; i < 3; i++) await Uploaded(toothVisit.VisitId, 51);
            using(var r = await Upload(toothVisit.VisitId, 51)) check(r.StatusCode == HttpStatusCode.BadRequest, "Fourth image for same FDI tooth blocked");
            var full = await Visit(); db.DentalImages.AddRange(Enumerable.Range(0,20).Select(_ => Dummy(full.VisitId))); await db.SaveChangesAsync();
            using(var r = await Upload(full.VisitId)) check(r.StatusCode == HttpStatusCode.BadRequest, "Twenty-first image blocked");
            var large = await Visit(); db.DentalImages.Add(Dummy(large.VisitId, 100L * 1024 * 1024)); await db.SaveChangesAsync();
            using(var r = await Upload(large.VisitId)) check(r.StatusCode == HttpStatusCode.BadRequest, "100MB per visit enforced");
            using(var r = await client.PostAsJsonAsync($"/api/dental-images/{late.Id}/attach", new AttachDentalImageRequest(large.VisitId, late.Version))) check(r.StatusCode == HttpStatusCode.BadRequest, "Attach also enforces accumulated byte quota");
            var firstTarget = await Visit(); var secondTarget = await Visit();
            var attaches = await Task.WhenAll(client.PostAsJsonAsync($"/api/dental-images/{late.Id}/attach", new AttachDentalImageRequest(firstTarget.VisitId, late.Version)), client.PostAsJsonAsync($"/api/dental-images/{late.Id}/attach", new AttachDentalImageRequest(secondTarget.VisitId, late.Version)));
            check(attaches.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && attaches.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "Concurrent attach has exactly one winner"); foreach(var r in attaches) r.Dispose();
            var raceVisit = await Visit(); db.DentalImages.AddRange(Enumerable.Range(0,19).Select(_ => Dummy(raceVisit.VisitId))); await db.SaveChangesAsync();
            var race = await Task.WhenAll(Upload(raceVisit.VisitId), Upload(raceVisit.VisitId)); foreach(var r in race) r.Dispose();
            check(race.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && await db.DentalImages.CountAsync(x => x.VisitId == raceVisit.VisitId) == 20, "Concurrent upload cannot exceed visit count");
            var filesBefore = FileCount(); var rowsBefore = await db.DentalImages.CountAsync(); failure.Fail = true;
            using(var r = await Upload()) check(r.StatusCode == HttpStatusCode.InternalServerError, "Audit failure rejects upload"); failure.Fail = false;
            check(FileCount() == filesBefore && await db.DentalImages.CountAsync() == rowsBefore, "Audit rollback removes database row and physical file");
            var inactive = users[RoleCodes.Assistant]; inactive.IsActive = false; await db.SaveChangesAsync(); As(RoleCodes.Assistant);
            using(var r = await Upload()) check(r.StatusCode == HttpStatusCode.NotFound, "Inactive actor cannot upload using stale JWT");
            As(RoleCodes.Dentist); var someoneElseVisit = await Visit(dentist: users[RoleCodes.Admin].UserId); using(var r = await Upload(someoneElseVisit.VisitId)) check(r.StatusCode == HttpStatusCode.BadRequest, "Dentist cannot upload into another dentist visit");
            using(var r = await client.GetAsync($"/api/dental-images/targets?patientId={patient.PatientId}"))
            { var items = (await r.Content.ReadFromJsonAsync<ImageVisitTarget[]>())!; check(items.Any(x => x.VisitId == visit.VisitId) && items.All(x => x.VisitId != locked.VisitId && x.VisitId != someoneElseVisit.VisitId), "Targets exclude locked or other dentist cases"); }
        }
        finally
        {
            await app.StopAsync();
            var full = Path.GetFullPath(root); var prefix = Path.GetFullPath(Path.GetTempPath());
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("dental-images-http-", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe test cleanup path");
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }
    }
}
