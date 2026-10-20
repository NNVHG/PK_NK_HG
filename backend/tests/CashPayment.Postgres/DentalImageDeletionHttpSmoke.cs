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

internal static class DentalImageDeletionHttpSmoke
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
        { var user = new User { Role = await db.Roles.FirstAsync(r=>r.RoleCode==role), FullName = "Synthetic deletion account", Phone = "096200000" + users.Count, PasswordHash = "synthetic-not-used" }; users[role] = user; db.Users.Add(user); }
        await db.SaveChangesAsync();
        var patient = new Patient { FullName = "Synthetic image patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0962000008", UserId = users[RoleCodes.Patient].UserId };
        var other = new Patient { FullName = "Synthetic other image patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0962000009" }; db.Patients.AddRange(patient, other); await db.SaveChangesAsync();
        async Task<Visit> Visit(string state = VisitStatuses.InProgress, int? patientId = null, int? dentist = null)
        { var v = new Visit { PatientId = patientId ?? patient.PatientId, Status = state, CreatedByUserId = users[RoleCodes.Admin].UserId, DentistId = dentist ?? users[RoleCodes.Dentist].UserId }; db.Visits.Add(v); await db.SaveChangesAsync(); return v; }
        void As(string? role) => client.DefaultRequestHeaders.Authorization = role is null ? null : new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(users[role]));
        var storage=new DentalImageStorage(root);
        var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a7KkAAAAASUVORK5CYII=");
        async Task<DentalImage> Image(string uploader=RoleCodes.Dentist,int? visit=null,int? owner=null){var id=owner??patient.PatientId;var key=$"{id}/{Guid.NewGuid():N}.png";await storage.WriteAsync(key,png,default);var image=new DentalImage {PatientId=id,VisitId=visit,OriginalFileName="synthetic.png",StoragePath=key,ContentType="image/png",SizeBytes=png.Length,Width=1,Height=1,UploadedByUserId=users[uploader].UserId,CreatedAt=DateTime.UtcNow,Notes="upload note",ClinicalNotes="clinical original",DoctorDiagnosis="clinical diagnosis"};db.DentalImages.Add(image);await db.SaveChangesAsync();return image;}
        string Url(int id)=>$"/api/dental-images/{id}";
        async Task<HttpResponseMessage> Delete(int id,int version=1,string reason="synthetic duplicate",bool confirmed=true){using var request=new HttpRequestMessage(HttpMethod.Delete,Url(id)){Content=JsonContent.Create(new DeleteDentalImageRequest(version,reason,confirmed))};return await client.SendAsync(request);}
        Task<HttpResponseMessage> List(int patientId,int before=0)=>client.GetAsync($"/api/dental-images/deletion?patientId={patientId}&beforeId={before}");
        async Task Status(Task<HttpResponseMessage> pending,HttpStatusCode expected,string label){using var r=await pending;check(r.StatusCode==expected,label+" ("+(int)r.StatusCode+")");}
        async Task<DentalImageDeletionPage> Page(){using var r=await List(patient.PatientId);check(r.StatusCode==HttpStatusCode.OK&&r.Headers.CacheControl?.NoStore==true,"Deletion metadata NoStore200");var text=await r.Content.ReadAsStringAsync();check(!text.Contains("storagePath",StringComparison.OrdinalIgnoreCase)&&!text.Contains("clinical original")&&!text.Contains("clinical diagnosis")&&!text.Contains("upload note"),"Deletion DTO excludes file path and clinical/upload notes");return (await r.Content.ReadFromJsonAsync<DentalImageDeletionPage>())!;}
        try {
            var open=await Visit();var owned=await Image(visit:open.VisitId);var assisted=await Image(RoleCodes.Assistant);var admin=await Image(RoleCodes.Admin);
            As(null);await Status(Delete(owned.Id),HttpStatusCode.Unauthorized,"Anonymous cannot delete");await Status(List(patient.PatientId),HttpStatusCode.Unauthorized,"Anonymous cannot read deletion metadata");
            foreach(var role in new[]{RoleCodes.Receptionist,RoleCodes.Patient}){As(role);await Status(Delete(owned.Id),HttpStatusCode.Forbidden,role+" cannot delete");await Status(List(patient.PatientId),HttpStatusCode.Forbidden,role+" cannot list deletion metadata");}
            As(RoleCodes.Dentist);var page=await Page();check(page.Items.Select(i=>i.Id).SequenceEqual(new[]{owned.Id}),"Dentist sees only own uploads for deletion");await Status(Delete(assisted.Id),HttpStatusCode.NotFound,"Responsible dentist cannot delete another uploader image");
            As(RoleCodes.Assistant);page=await Page();check(page.Items.Select(i=>i.Id).SequenceEqual(new[]{assisted.Id})&&page.Items.Single().CanDelete,"Assistant sees own minimal metadata only");await Status(client.GetAsync(Url(assisted.Id)+"/file"),HttpStatusCode.Forbidden,"Delete permission never grants assistant stream");await Status(client.GetAsync(Url(assisted.Id)+"/clinical-notes"),HttpStatusCode.Forbidden,"Delete permission never grants assistant clinical read");await Status(Delete(owned.Id),HttpStatusCode.NotFound,"Assistant cannot delete another uploader image");
            As(RoleCodes.Admin);page=await Page();check(page.Items.Length==3&&page.Items.All(i=>i.CanDelete),"Admin sees all eligible images");
            foreach(var request in new[]{new DeleteDentalImageRequest(0,"reason",true),new(int.MaxValue,"reason",true),new(1,"",true),new(1,new string('x',501),true),new(1,"a\0b",true),new(1,"reason",false)}){using var http=new HttpRequestMessage(HttpMethod.Delete,Url(admin.Id)){Content=JsonContent.Create(request)};await Status(client.SendAsync(http),HttpStatusCode.BadRequest,"Delete validates reason/version/confirmation");}
            failure.Fail=true;await Status(Delete(admin.Id),HttpStatusCode.InternalServerError,"Delete audit failure rejects write");failure.Fail=false;await db.Entry(admin).ReloadAsync();check(!admin.IsDeleted&&admin.Version==1&&admin.DeletedAt is null&&admin.DeleteReason is null,"Audit failure rolls back deletion metadata/version");
            var count=await db.AuditLogs.CountAsync(a=>a.Action=="MOD_IMG_SOFT_DELETED");await Status(Delete(admin.Id,2),HttpStatusCode.Conflict,"Stale version cannot delete");check(await db.AuditLogs.CountAsync(a=>a.Action=="MOD_IMG_SOFT_DELETED")==count,"Rejected deletion adds no audit");
            As(RoleCodes.Assistant);using(var r=await Delete(assisted.Id,reason:"  synthetic duplicate  "))check(r.StatusCode==HttpStatusCode.NoContent&&r.Headers.CacheControl?.NoStore==true,"Uploader assistant deletes own image NoStore204");await db.Entry(assisted).ReloadAsync();check(assisted.IsDeleted&&assisted.DeletedByUserId==users[RoleCodes.Assistant].UserId&&assisted.DeletedAt?.Kind==DateTimeKind.Utc&&assisted.DeleteReason=="synthetic duplicate"&&assisted.Version==2,"Deletion records real actor/time/trimmed reason/version");
            await using(var stream=await storage.OpenReadAsync(assisted.StoragePath,default)){using var bytes=new MemoryStream();await stream.CopyToAsync(bytes);check(bytes.ToArray().SequenceEqual(png),"Soft deletion preserves original physical bytes");}
            await Status(Delete(assisted.Id),HttpStatusCode.NotFound,"Repeated delete does not reapply metadata");check(await db.AuditLogs.CountAsync(a=>a.Action=="MOD_IMG_SOFT_DELETED"&&a.EntityId==assisted.Id)==1,"Repeated delete never duplicates audit");
            As(RoleCodes.Admin);await Status(client.GetAsync(Url(assisted.Id)+"/file"),HttpStatusCode.NotFound,"Deleted image stream inaccessible");await Status(client.GetAsync(Url(assisted.Id)+"/clinical-notes"),HttpStatusCode.NotFound,"Deleted clinical original inaccessible");await Status(client.GetAsync(Url(assisted.Id)+"/clinical-notes/addenda"),HttpStatusCode.NotFound,"Deleted clinical history inaccessible");await Status(client.PostAsJsonAsync(Url(assisted.Id)+"/attach",new AttachDentalImageRequest(open.VisitId,2)),HttpStatusCode.NotFound,"Deleted image cannot attach");
            var gallery=await client.GetFromJsonAsync<DentalImagePage>($"/api/dental-images?patientId={patient.PatientId}");check(!gallery!.Items.Any(i=>i.Id==assisted.Id)&&!(await Page()).Items.Any(i=>i.Id==assisted.Id),"Deleted metadata disappears from gallery and deletion list");
            await Status(Delete(owned.Id),HttpStatusCode.NoContent,"Admin deletes another uploader image");await db.Entry(owned).ReloadAsync();check(owned.ClinicalNotes=="clinical original"&&owned.DoctorDiagnosis=="clinical diagnosis"&&owned.Notes=="upload note"&&owned.VisitId==open.VisitId,"Soft delete preserves clinical original/upload note/link");
            var independent=await Image();As(RoleCodes.Dentist);await Status(Delete(independent.Id),HttpStatusCode.NoContent,"Dentist uploader deletes own independent image");
            foreach(var state in new[]{VisitStatuses.Created,VisitStatuses.Completed,VisitStatuses.Cancelled,VisitStatuses.Correcting}){var v=await Visit(state);var i=await Image(visit:v.VisitId);As(RoleCodes.Admin);await Status(Delete(i.Id),HttpStatusCode.Conflict,state+" blocks delete even Admin");check(!(await Page()).Items.Single(x=>x.Id==i.Id).CanDelete,state+" is read-only in deletion context");}
            var lockedVisit=await Visit();lockedVisit.IsLocked=true;await db.SaveChangesAsync();var locked=await Image(visit:lockedVisit.VisitId);await Status(Delete(locked.Id),HttpStatusCode.Conflict,"Locked InProgress cannot delete");
            var paidVisit=await Visit();var paid=await Image(visit:paidVisit.VisitId);var invoice=new Invoice {VisitId=paidVisit.VisitId,CreatedByUserId=users[RoleCodes.Admin].UserId,InvoiceCode="DELETE-TEST",Status=InvoiceStatus.Paid,TotalAmount=1000,PaidAmount=1000};db.Invoices.Add(invoice);await db.SaveChangesAsync();await Status(Delete(paid.Id),HttpStatusCode.Conflict,"Paid InProgress cannot delete");invoice.Status=InvoiceStatus.PendingPayment;invoice.PaidAmount=0;db.PaymentTransactions.Add(new PaymentTransaction {InvoiceId=invoice.Id,Amount=1000,CashierId=users[RoleCodes.Admin].UserId,PaidAt=DateTime.UtcNow,RequestId=Guid.NewGuid()});await db.SaveChangesAsync();await Status(Delete(paid.Id),HttpStatusCode.Conflict,"Payment history blocks deletion independently of invoice summary");
            var stale=await Image();As(RoleCodes.Dentist);using(var update=await client.PutAsJsonAsync(Url(stale.Id)+"/clinical-notes",new ImageClinicalWriteRequest(1,"updated",null,true)))check(update.StatusCode==HttpStatusCode.NotFound,"Dentist cannot overwrite clinical notes before linking");As(RoleCodes.Admin);using(var update=await client.PutAsJsonAsync(Url(stale.Id)+"/clinical-notes",new ImageClinicalWriteRequest(1,"updated",null,true)))check(update.StatusCode==HttpStatusCode.OK,"Clinical update changes shared version");await Status(Delete(stale.Id),HttpStatusCode.Conflict,"Clinical change invalidates deletion confirmation");await Status(Delete(stale.Id,2),HttpStatusCode.NoContent,"Reloaded exact version allows delete");
            var concurrent=await Image();var races=await Task.WhenAll(Delete(concurrent.Id),Delete(concurrent.Id));check(races.Count(x=>x.StatusCode==HttpStatusCode.NoContent)==1&&races.All(x=>x.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict or HttpStatusCode.NotFound),"Concurrent delete has one winner");foreach(var r in races)r.Dispose();check(await db.AuditLogs.CountAsync(a=>a.Action=="MOD_IMG_SOFT_DELETED"&&a.EntityId==concurrent.Id)==1,"Concurrent delete writes one audit");
            var inactive=await Image(RoleCodes.Assistant);As(RoleCodes.Assistant);users[RoleCodes.Assistant].IsActive=false;await db.SaveChangesAsync();await Status(Delete(inactive.Id),HttpStatusCode.NotFound,"Inactive actor cannot delete with stale JWT");await Status(List(patient.PatientId),HttpStatusCode.NotFound,"Inactive actor cannot list deletion metadata");users[RoleCodes.Assistant].IsActive=true;users[RoleCodes.Assistant].Role=users[RoleCodes.Receptionist].Role;await db.SaveChangesAsync();await Status(Delete(inactive.Id),HttpStatusCode.NotFound,"Actual DB role checked beyond stale JWT");
            As(RoleCodes.Admin);patient.IsActive=false;await db.SaveChangesAsync();await Status(Delete(inactive.Id),HttpStatusCode.NotFound,"Inactive patient prevents delete");await Status(List(patient.PatientId),HttpStatusCode.NotFound,"Inactive patient metadata hidden");patient.IsActive=true;await db.SaveChangesAsync();
            await Status(List(0),HttpStatusCode.BadRequest,"Invalid patient rejected");await Status(List(patient.PatientId,-1),HttpStatusCode.BadRequest,"Invalid cursor rejected");
            for(var n=0;n<22;n++)await Image(RoleCodes.Admin);page=await Page();check(page.Items.Length==20&&page.HasMore,"Deletion list bounded to20 with cursor");using(var next=await List(patient.PatientId,page.Items.Last().Id)){var tail=(await next.Content.ReadFromJsonAsync<DentalImageDeletionPage>())!;check(next.StatusCode==HttpStatusCode.OK&&tail.Items.All(i=>i.Id<page.Items.Last().Id),"Deletion cursor never repeats image");}
        }finally {failure.Fail=false;await app.StopAsync();var absolute=Path.GetFullPath(root);if(Directory.Exists(absolute)&&absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(absolute).StartsWith("dental-images-http-",StringComparison.Ordinal))Directory.Delete(absolute,true);}
    }
}
