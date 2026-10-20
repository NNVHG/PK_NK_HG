using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Api.RateLimiting;
using Dental.Application.Features.MOD_IMG;
using Dental.Application.Common;
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

internal static class DentalImageMetadataHttpSmoke
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
        builder.Services.AddSingleton(TimeProvider.System); builder.Services.AddSingleton<VietnamClock>();
        builder.Services.AddScoped<IDentalImageDeletionRepository,DentalImageDeletionRepository>();builder.Services.AddScoped<DentalImageDeletionService>();
        builder.Services.AddScoped<IDentalImageClinicalRepository,DentalImageClinicalRepository>();builder.Services.AddScoped<DentalImageClinicalService>();
        builder.Services.AddScoped<IDentalImageMetadataRepository,DentalImageMetadataRepository>();builder.Services.AddScoped<DentalImageMetadataService>();
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
        { var user = new User { Role = await db.Roles.FirstAsync(r=>r.RoleCode==role), FullName = "Synthetic deletion account", Phone = "096300000" + users.Count, PasswordHash = "synthetic-not-used" }; users[role] = user; db.Users.Add(user); }
        await db.SaveChangesAsync();
        var patient = new Patient { FullName = "Synthetic image patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0963000008", UserId = users[RoleCodes.Patient].UserId };
        var other = new Patient { FullName = "Synthetic other image patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0963000009" }; db.Patients.AddRange(patient, other); await db.SaveChangesAsync();
        async Task<Visit> Visit(string state = VisitStatuses.InProgress, int? patientId = null, int? dentist = null)
        { var v = new Visit { PatientId = patientId ?? patient.PatientId, Status = state, CreatedByUserId = users[RoleCodes.Admin].UserId, DentistId = dentist ?? users[RoleCodes.Dentist].UserId }; db.Visits.Add(v); await db.SaveChangesAsync(); return v; }
        void As(string? role) => client.DefaultRequestHeaders.Authorization = role is null ? null : new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(users[role]));
        var storage=new DentalImageStorage(root);var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a7KkAAAAASUVORK5CYII=");
        async Task<DentalImage> Image(int? visit=null,int? tooth=null,int? patientId=null){var id=patientId??patient.PatientId;var key=$"{id}/{Guid.NewGuid():N}.png";await storage.WriteAsync(key,png,default);var i=new DentalImage {PatientId=id,VisitId=visit,ToothNumber=tooth,Category=DentalImageCategory.Intraoral,Notes="upload original",ClinicalNotes="clinical original",DoctorDiagnosis="diagnosis original",StoragePath=key,OriginalFileName="synthetic.png",ContentType="image/png",Width=1,Height=1,SizeBytes=png.Length,UploadedByUserId=users[RoleCodes.Dentist].UserId,CreatedAt=DateTime.UtcNow};db.DentalImages.Add(i);await db.SaveChangesAsync();return i;}
        string Url(int id)=>$"/api/dental-images/{id}";
        Task<HttpResponseMessage> Update(int id,int version=1,string category="Periapical",int? tooth=46,string? notes="synthetic metadata",bool confirmed=true)=>client.PutAsJsonAsync(Url(id)+"/metadata",new UpdateDentalImageMetadataRequest(version,category,tooth,notes,confirmed));
        async Task Status(Task<HttpResponseMessage> pending,HttpStatusCode expected,string label){using var r=await pending;check(r.StatusCode==expected,label+" ("+(int)r.StatusCode+")");}
        async Task<DentalImageMetadataContext> Read(int id){using var r=await client.GetAsync(Url(id)+"/metadata");check(r.StatusCode==HttpStatusCode.OK&&r.Headers.CacheControl?.NoStore==true,"Metadata context NoStore200");var text=await r.Content.ReadAsStringAsync();check(!text.Contains("storagePath",StringComparison.OrdinalIgnoreCase)&&!text.Contains("clinical original")&&!text.Contains("diagnosis original"),"Metadata DTO never exposes file path or clinical content");return (await r.Content.ReadFromJsonAsync<DentalImageMetadataContext>())!;}
        try {
            var visit=await Visit();var image=await Image(visit.VisitId,46);var independent=await Image();
            As(null);await Status(client.GetAsync(Url(image.Id)+"/metadata"),HttpStatusCode.Unauthorized,"Anonymous metadata read denied");await Status(Update(image.Id),HttpStatusCode.Unauthorized,"Anonymous metadata write denied");
            foreach(var role in new[]{RoleCodes.Assistant,RoleCodes.Receptionist,RoleCodes.Patient}){As(role);await Status(client.GetAsync(Url(image.Id)+"/metadata"),HttpStatusCode.Forbidden,role+" metadata editor read denied");await Status(Update(image.Id),HttpStatusCode.Forbidden,role+" metadata write denied");}
            As(RoleCodes.Dentist);check(!(await Read(independent.Id)).CanEdit,"Independent dentist metadata read-only Q338a");await Status(Update(independent.Id),HttpStatusCode.NotFound,"Independent metadata only Admin writes");check((await Read(image.Id)).CanEdit,"Responsible dentist may edit active Visit metadata");
            foreach(var bad in new[]{new UpdateDentalImageMetadataRequest(0,"Intraoral",46,null,true),new(int.MaxValue,"Intraoral",46,null,true),new(1,"Other",46,null,true),new(1,"Panoramic",46,null,true),new(1,"Intraoral",19,null,true),new(1,"Intraoral",56,null,true),new(1,"Intraoral",46,new string('x',501),true),new(1,"Intraoral",46,"a\0b",true),new(1,"Intraoral",46,null,false)})await Status(client.PutAsJsonAsync(Url(image.Id)+"/metadata",bad),HttpStatusCode.BadRequest,"Metadata whitelist/FDI/notes/version/confirmation enforced");
            using(var r=await Update(image.Id,notes:"  synthetic metadata  ")){var saved=await r.Content.ReadFromJsonAsync<DentalImageMetadataContext>();check(r.StatusCode==HttpStatusCode.OK&&r.Headers.CacheControl?.NoStore==true&&saved!.Version==2&&saved.Category=="Periapical"&&saved.ToothNumber==46&&saved.Notes=="synthetic metadata","Dentist saves trimmed metadata with incremented version");}
            await db.Entry(image).ReloadAsync();check(image.VisitId==visit.VisitId&&image.ClinicalNotes=="clinical original"&&image.DoctorDiagnosis=="diagnosis original"&&image.UploadedByUserId==users[RoleCodes.Dentist].UserId&&image.OriginalFileName=="synthetic.png","Metadata preserves Visit/clinical/uploader/file identity");await using(var file=await storage.OpenReadAsync(image.StoragePath,default)){using var memory=new MemoryStream();await file.CopyToAsync(memory);check(memory.ToArray().SequenceEqual(png),"Metadata leaves physical bytes intact");}
            var audit=await db.AuditLogs.SingleAsync(a=>a.Action=="MOD_IMG_METADATA_UPDATED"&&a.EntityId==image.Id);check(audit.UserId==users[RoleCodes.Dentist].UserId&&!audit.Detail!.Contains("synthetic metadata")&&!audit.Detail.Contains("clinical original")&&audit.Detail.Contains("notesChanged"),"Audit stores actor and field metadata without free text");
            await Status(Update(image.Id),HttpStatusCode.Conflict,"Stale metadata version rejected");failure.Fail=true;await Status(Update(image.Id,2,notes:"rollback"),HttpStatusCode.InternalServerError,"Metadata audit failure aborts write");failure.Fail=false;await db.Entry(image).ReloadAsync();check(image.Version==2&&image.Notes=="synthetic metadata"&&await db.AuditLogs.CountAsync(a=>a.Action=="MOD_IMG_METADATA_UPDATED"&&a.EntityId==image.Id)==1,"Metadata/version/audit rollback together");
            await Status(Update(image.Id,2,"Panorama",null," "),HttpStatusCode.OK,"Optional tooth and notes can clear");await db.Entry(image).ReloadAsync();check(image.ToothNumber is null&&image.Notes is null,"Blank metadata normalizes to null");
            As(RoleCodes.Admin);await Status(Update(independent.Id,category:"Bitewing",tooth:85),HttpStatusCode.OK,"Admin edits independent image with child FDI");
            var oldDentist=visit.DentistId;visit.DentistId=users[RoleCodes.Admin].UserId;await db.SaveChangesAsync();As(RoleCodes.Dentist);check(!(await Read(image.Id)).CanEdit,"Nonresponsible dentist metadata flags read-only");await Status(Update(image.Id,3),HttpStatusCode.NotFound,"Visit ownership controls metadata independently of uploader");visit.DentistId=oldDentist;await db.SaveChangesAsync();
            As(RoleCodes.Admin);foreach(var state in new[]{VisitStatuses.Created,VisitStatuses.Completed,VisitStatuses.Cancelled}){visit.Status=state;await db.SaveChangesAsync();check(!(await Read(image.Id)).CanEdit,state+" metadata context locked");await Status(Update(image.Id,3),HttpStatusCode.Conflict,state+" prevents metadata overwrite even Admin");}
            visit.Status=VisitStatuses.Correcting;visit.IsLocked=true;await db.SaveChangesAsync();await Status(Update(image.Id,3),HttpStatusCode.Conflict,"Locked Correcting metadata blocked");visit.IsLocked=false;await db.SaveChangesAsync();await Status(Update(image.Id,3),HttpStatusCode.OK,"Unlocked unpaid Correcting metadata allowed");
            var invoice=new Invoice {VisitId=visit.VisitId,CreatedByUserId=users[RoleCodes.Admin].UserId,InvoiceCode="META-TEST",Status=InvoiceStatus.Paid,TotalAmount=1000,PaidAmount=1000};db.Invoices.Add(invoice);await db.SaveChangesAsync();await Status(Update(image.Id,4),HttpStatusCode.Conflict,"Paid prevents metadata edit in Correcting");invoice.Status=InvoiceStatus.PendingPayment;invoice.PaidAmount=0;db.PaymentTransactions.Add(new PaymentTransaction {InvoiceId=invoice.Id,Amount=1000,CashierId=users[RoleCodes.Admin].UserId,PaidAt=DateTime.UtcNow,RequestId=Guid.NewGuid()});await db.SaveChangesAsync();await Status(Update(image.Id,4),HttpStatusCode.Conflict,"Historical receipt locks metadata beyond status summary");
            var capacityVisit=await Visit();var fixed1=await Image(capacityVisit.VisitId,11);var fixed2=await Image(capacityVisit.VisitId,11);var fixed3=await Image(capacityVisit.VisitId,11);var moving=await Image(capacityVisit.VisitId,46);
            await Status(Update(moving.Id,tooth:11),HttpStatusCode.Conflict,"Moving to occupied tooth above3 is blocked");await Status(Update(fixed1.Id,tooth:11),HttpStatusCode.OK,"Current image is excluded from its own tooth quota");fixed3.IsDeleted=true;await db.SaveChangesAsync();await Status(Update(moving.Id,tooth:11),HttpStatusCode.OK,"Soft-deleted images do not consume active tooth count");
            var quotaRaceVisit=await Visit();await Image(quotaRaceVisit.VisitId,21);await Image(quotaRaceVisit.VisitId,21);var raceA=await Image(quotaRaceVisit.VisitId,46);var raceB=await Image(quotaRaceVisit.VisitId,47);var quotaRace=await Task.WhenAll(Update(raceA.Id,tooth:21),Update(raceB.Id,tooth:21));check(quotaRace.Count(r=>r.StatusCode==HttpStatusCode.OK)==1&&quotaRace.Count(r=>r.StatusCode==HttpStatusCode.Conflict)==1,"Serializable tooth quota race has one winner");foreach(var r in quotaRace)r.Dispose();check(await db.DentalImages.CountAsync(i=>i.VisitId==quotaRaceVisit.VisitId&&!i.IsDeleted&&i.ToothNumber==21)==3,"Concurrent metadata never exceeds3 images per tooth");
            var concurrent=await Image();var race=await Task.WhenAll(Update(concurrent.Id,notes:"one"),Update(concurrent.Id,notes:"two"));check(race.Count(r=>r.StatusCode==HttpStatusCode.OK)==1&&race.Count(r=>r.StatusCode==HttpStatusCode.Conflict)==1,"Concurrent metadata update has one winner");foreach(var r in race)r.Dispose();check(await db.AuditLogs.CountAsync(a=>a.EntityId==concurrent.Id&&a.Action=="MOD_IMG_METADATA_UPDATED")==1,"Concurrent metadata creates one audit");
            var attaching=await Image(tooth:48);var attachVisit=await Visit();var metadataRace=Update(attaching.Id,tooth:51);var attachRace=client.PostAsJsonAsync(Url(attaching.Id)+"/attach",new AttachDentalImageRequest(attachVisit.VisitId,1));var attached=await Task.WhenAll(metadataRace,attachRace);check(attached.Count(r=>r.StatusCode==HttpStatusCode.OK)==1&&attached.Count(r=>r.StatusCode==HttpStatusCode.Conflict)==1,"Attach and metadata share one version guard");foreach(var r in attached)r.Dispose();
            var deleting=await Image();using(var request=new HttpRequestMessage(HttpMethod.Delete,Url(deleting.Id)){Content=JsonContent.Create(new DeleteDentalImageRequest(1,"synthetic duplicate",true))}){var changes=await Task.WhenAll(Update(deleting.Id),client.SendAsync(request));check(changes.Count(r=>r.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent)==1&&changes.All(r=>r.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent or HttpStatusCode.NotFound or HttpStatusCode.Conflict),"Delete and metadata cannot both apply stale confirmation");foreach(var r in changes)r.Dispose();}
            var unchanged=await Image();var unchangedPath=unchanged.StoragePath;await Status(client.PutAsJsonAsync(Url(unchanged.Id)+"/metadata",new {expectedVersion=1,category="Panorama",toothNumber=(int?)null,notes="technical",confirmed=true,patientId=other.PatientId,visitId=visit.VisitId,clinicalNotes="overwrite",storagePath="other.png",originalFileName="replacement.png",doctorDiagnosis="overwrite"}),HttpStatusCode.OK,"Extra JSON fields never expand metadata mutation");await db.Entry(unchanged).ReloadAsync();check(unchanged.PatientId==patient.PatientId&&unchanged.VisitId is null&&unchanged.ClinicalNotes=="clinical original"&&unchanged.StoragePath==unchangedPath&&unchanged.OriginalFileName=="synthetic.png"&&unchanged.DoctorDiagnosis=="diagnosis original","Patient/Visit/clinical/path cannot be edited through metadata DTO");
            var erased=await Image();erased.IsDeleted=true;await db.SaveChangesAsync();await Status(client.GetAsync(Url(erased.Id)+"/metadata"),HttpStatusCode.NotFound,"Deleted metadata read denied");await Status(Update(erased.Id),HttpStatusCode.NotFound,"Deleted metadata write denied");
            As(RoleCodes.Dentist);users[RoleCodes.Dentist].IsActive=false;await db.SaveChangesAsync();await Status(Update(moving.Id,2),HttpStatusCode.NotFound,"Inactive actor cannot edit with stale JWT");await Status(client.GetAsync(Url(moving.Id)+"/metadata"),HttpStatusCode.NotFound,"Inactive actor cannot read editor context");users[RoleCodes.Dentist].IsActive=true;users[RoleCodes.Dentist].Role=users[RoleCodes.Assistant].Role;await db.SaveChangesAsync();await Status(Update(moving.Id,2),HttpStatusCode.NotFound,"Actual DB role required beyond stale claim");As(RoleCodes.Admin);patient.IsActive=false;await db.SaveChangesAsync();await Status(Update(moving.Id,2),HttpStatusCode.NotFound,"Inactive patient metadata concealed");
        }finally{failure.Fail=false;await app.StopAsync();var absolute=Path.GetFullPath(root);if(Directory.Exists(absolute)&&absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(absolute).StartsWith("dental-images-http-",StringComparison.Ordinal))Directory.Delete(absolute,true);}
    }
}
