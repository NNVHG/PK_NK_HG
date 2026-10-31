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

internal static class DentalImageClinicalHttpSmoke
{
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Fail && data.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action.StartsWith("MOD_IMG_CLINICAL"))) throw new InvalidOperationException("Synthetic audit failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool, string> check)
    {
        var failure = new FailAudit();
        var dbOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure).Options;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); builder.Configuration["Jwt:Secret"] = secret;
        builder.Services.AddScoped(_ => new DentalDbContext(dbOptions)); builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton(TimeProvider.System); builder.Services.AddSingleton<VietnamClock>();
        builder.Services.AddScoped<IDentalImageClinicalRepository, DentalImageClinicalRepository>(); builder.Services.AddScoped<DentalImageClinicalService>();
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
        { var user = new User { Role = await db.Roles.FirstAsync(r=>r.RoleCode==role), FullName = "Synthetic clinical account", Phone = "096100000" + users.Count, PasswordHash = "synthetic-not-used" }; users[role] = user; db.Users.Add(user); }
        await db.SaveChangesAsync();
        var patient = new Patient { FullName = "Synthetic image patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0961000008", UserId = users[RoleCodes.Patient].UserId };
        var other = new Patient { FullName = "Synthetic other image patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0961000009" }; db.Patients.AddRange(patient, other); await db.SaveChangesAsync();
        async Task<Visit> Visit(string state = VisitStatuses.InProgress, int? patientId = null, int? dentist = null)
        { var v = new Visit { PatientId = patientId ?? patient.PatientId, Status = state, CreatedByUserId = users[RoleCodes.Admin].UserId, DentistId = dentist ?? users[RoleCodes.Dentist].UserId }; db.Visits.Add(v); await db.SaveChangesAsync(); return v; }
        void As(string? role) => client.DefaultRequestHeaders.Authorization = role is null ? null : new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(users[role]));
        var extra = new User {Role=users[RoleCodes.Dentist].Role,FullName="Synthetic other dentist",Phone="0961000010",PasswordHash="unused"}; db.Users.Add(extra); await db.SaveChangesAsync();
        async Task<DentalImage> Image(int? visit=null, int? owner=null){var i=new DentalImage {PatientId=owner??patient.PatientId,VisitId=visit,Notes="upload description",StoragePath="synthetic/"+Guid.NewGuid(),OriginalFileName="synthetic.png",ContentType="image/png",SizeBytes=20,Width=1,Height=1,CreatedAt=DateTime.UtcNow,UploadedByUserId=users[RoleCodes.Admin].UserId};db.DentalImages.Add(i);await db.SaveChangesAsync();return i;}
        string Url(int id)=>$"/api/dental-images/{id}/clinical-notes";
        Task<HttpResponseMessage> Put(int id,int version=1,string? text="synthetic note",string? diagnosis="synthetic diagnosis",bool confirmed=true)=>client.PutAsJsonAsync(Url(id),new ImageClinicalWriteRequest(version,text,diagnosis,confirmed));
        Task<HttpResponseMessage> Append(int id,ImageClinicalAppendRequest request)=>client.PostAsJsonAsync(Url(id)+"/addenda",request);
        async Task Status(Task<HttpResponseMessage> response,HttpStatusCode expected,string label){using var r=await response;check(r.StatusCode==expected,label+" ("+(int)r.StatusCode+")");}
        async Task<ImageClinicalContext> Read(int id){using var r=await client.GetAsync(Url(id));check(r.StatusCode==HttpStatusCode.OK&&r.Headers.CacheControl?.NoStore==true,"Read NoStore200");var text=await r.Content.ReadAsStringAsync();check(!text.Contains("storagePath",StringComparison.OrdinalIgnoreCase),"Read never exposes storage path");return (await r.Content.ReadFromJsonAsync<ImageClinicalContext>())!;}
        try {
            var open=await Visit();var linked=await Image(open.VisitId);var independent=await Image();var foreign=await Image(owner:other.PatientId);
            As(null);await Status(client.GetAsync(Url(linked.Id)),HttpStatusCode.Unauthorized,"Anonymous read denied");await Status(Put(linked.Id),HttpStatusCode.Unauthorized,"Anonymous write denied");await Status(client.GetAsync(Url(linked.Id)+"/addenda"),HttpStatusCode.Unauthorized,"Anonymous history denied");
            foreach(var role in new[]{RoleCodes.Assistant,RoleCodes.Patient,RoleCodes.Receptionist}){As(role);await Status(Put(linked.Id),HttpStatusCode.Forbidden,role+" write denied");await Status(Append(linked.Id,new(1,"note",Guid.NewGuid(),true)),HttpStatusCode.Forbidden,role+" append denied");}
            As(RoleCodes.Assistant);await Status(client.GetAsync(Url(linked.Id)),HttpStatusCode.Forbidden,"Assistant read denied");
            foreach(var role in new[]{RoleCodes.Patient,RoleCodes.Receptionist}){As(role);var c=await Read(linked.Id);check(!c.CanReplace&&!c.CanAppend,"Reader flags cannot write");await Status(client.GetAsync(Url(linked.Id)+"/addenda"),HttpStatusCode.OK,"Reader history allowed");}
            As(RoleCodes.Patient);await Status(client.GetAsync(Url(foreign.Id)),HttpStatusCode.NotFound,"Patient cannot read other patient");await Status(client.GetAsync(Url(foreign.Id)+"/addenda"),HttpStatusCode.NotFound,"Patient cannot read foreign history");
            As(RoleCodes.Dentist);check(!(await Read(independent.Id)).CanReplace,"Q336a unattached dentist read-only");await Status(Put(independent.Id),HttpStatusCode.NotFound,"Q336a dentist cannot edit unattached image");
            As(RoleCodes.Admin);check((await Read(independent.Id)).CanReplace,"Q336a Admin can write unattached image");await Status(Put(independent.Id),HttpStatusCode.OK,"Admin edits unattached image");
            await db.Entry(independent).ReloadAsync();check(independent.ClinicalNotes=="synthetic note"&&independent.Notes=="upload description"&&independent.Version==2,"Clinical fields separate from upload note, version increments");
            As(RoleCodes.Dentist);check((await Read(linked.Id)).CanReplace,"Responsible dentist edits open Visit");
            foreach(var invalid in new[]{new ImageClinicalWriteRequest(0,"n",null,true),new(1,new string('x',1001),null,true),new(1,null,new string('x',256),true),new(1,"n",null,false),new(1,"n\0",null,true),new(int.MaxValue,null,null,true)})await Status(client.PutAsJsonAsync(Url(linked.Id),invalid),HttpStatusCode.BadRequest,"Input bounds enforced");
            await Status(Put(linked.Id),HttpStatusCode.OK,"Dentist saves original");await Status(Put(linked.Id),HttpStatusCode.Conflict,"Stale original version blocked");
            await db.Entry(linked).ReloadAsync();check(linked.ClinicalUpdatedByUserId==users[RoleCodes.Dentist].UserId&&linked.ClinicalUpdatedAt?.Kind==DateTimeKind.Utc,"Original author and UTC timestamp stored");
            var audits=await db.AuditLogs.Where(a=>a.EntityId==linked.Id&&a.Action.StartsWith("MOD_IMG_CLINICAL")).ToArrayAsync();check(audits.Length==1&&audits.All(a=>!a.Detail!.Contains("synthetic note")&&!a.Detail.Contains("synthetic diagnosis")),"Audit records metadata only");
            client.DefaultRequestHeaders.Authorization=new("Bearer",tokens.GenerateAccessToken(extra));check(!(await Read(linked.Id)).CanReplace,"Colleague reads without edit permission");await Status(Put(linked.Id,2),HttpStatusCode.NotFound,"Colleague cannot overwrite");As(RoleCodes.Dentist);
            failure.Fail=true;await Status(Put(linked.Id,2,"rollback"),HttpStatusCode.InternalServerError,"Audit failure rejects original write");failure.Fail=false;await db.Entry(linked).ReloadAsync();check(linked.Version==2&&linked.ClinicalNotes=="synthetic note","Original write rolls back with failed audit");
            foreach(var state in new[]{VisitStatuses.Created,VisitStatuses.Cancelled}){open.Status=state;await db.SaveChangesAsync();await Status(Put(linked.Id,2),HttpStatusCode.Conflict,state+" original locked");await Status(Append(linked.Id,new(2,"n",Guid.NewGuid(),true)),HttpStatusCode.Conflict,state+" append locked");}
            open.Status=VisitStatuses.Correcting;open.IsLocked=true;await db.SaveChangesAsync();await Status(Put(linked.Id,2),HttpStatusCode.Conflict,"Locked Correcting cannot overwrite");open.IsLocked=false;await db.SaveChangesAsync();await Status(Put(linked.Id,2),HttpStatusCode.OK,"Unlocked Correcting permits original");
            var invoice=new Invoice {VisitId=open.VisitId,CreatedByUserId=users[RoleCodes.Admin].UserId,InvoiceCode="CLINICAL-TEST",Status=InvoiceStatus.Paid,TotalAmount=1000,PaidAmount=1000};db.Invoices.Add(invoice);await db.SaveChangesAsync();await Status(Put(linked.Id,3),HttpStatusCode.Conflict,"Paid prevents original overwrite even in Correcting");
            open.Status=VisitStatuses.Completed;open.IsLocked=true;await db.SaveChangesAsync();check((await Read(linked.Id)).CanAppend,"Completed paid permits append");As(RoleCodes.Admin);await Status(Put(linked.Id,3),HttpStatusCode.Conflict,"Completed Admin cannot overwrite");As(RoleCodes.Dentist);
            var request=new ImageClinicalAppendRequest(3," follow up ",Guid.NewGuid(),true);
            failure.Fail=true;await Status(Append(linked.Id,request),HttpStatusCode.InternalServerError,"Append audit failure rejects write");failure.Fail=false;await db.Entry(linked).ReloadAsync();check(linked.Version==3&&!await db.DentalImageClinicalAddenda.AnyAsync(a=>a.DentalImageId==linked.Id),"Failed append rolls back row and image version");
            using var saved=await Append(linked.Id,request);check(saved.StatusCode==HttpStatusCode.OK&&saved.Headers.CacheControl?.NoStore==true,"Append returns NoStore200");var entry=(await saved.Content.ReadFromJsonAsync<ImageClinicalAddendumDto>())!;check(entry.ImageVersion==4&&entry.Note=="follow up"&&entry.CreatedByUserId==users[RoleCodes.Dentist].UserId&&entry.AuthorRole==RoleCodes.Dentist,"Append author, role, trim and version");
            using(var replay=await Append(linked.Id,request)){var same=await replay.Content.ReadFromJsonAsync<ImageClinicalAddendumDto>();check(replay.StatusCode==HttpStatusCode.OK&&same!.Id==entry.Id,"Exact replay returns same immutable row");}
            await Status(Append(linked.Id,request with{Note="changed"}),HttpStatusCode.Conflict,"RequestId payload collision blocked");As(RoleCodes.Admin);await Status(Append(linked.Id,request),HttpStatusCode.Conflict,"RequestId actor collision blocked");As(RoleCodes.Dentist);
            check(await db.DentalImageClinicalAddenda.CountAsync(a=>a.DentalImageId==linked.Id)==1&&await db.AuditLogs.CountAsync(a=>a.EntityId==linked.Id&&a.Action=="MOD_IMG_CLINICAL_APPENDED")==1,"Replay never duplicates note or audit");
            users[RoleCodes.Dentist].FullName="Synthetic renamed dentist";await db.SaveChangesAsync();var history=await client.GetFromJsonAsync<PagedResult<ImageClinicalAddendumDto>>(Url(linked.Id)+"/addenda");check(history!.Items.Single().AuthorName=="Synthetic clinical account"&&history.PageSize==20,"Append preserves author snapshot and paginates");
            foreach(var query in new[]{"page=0","pageSize=51","page=1000001"})await Status(client.GetAsync(Url(linked.Id)+"/addenda?"+query),HttpStatusCode.BadRequest,"History bounds rejected");
            await db.Entry(linked).ReloadAsync();await db.Entry(open).ReloadAsync();await db.Entry(invoice).ReloadAsync();check(linked.ClinicalNotes=="synthetic note"&&linked.DoctorDiagnosis=="synthetic diagnosis"&&linked.Notes=="upload description"&&open.Status==VisitStatuses.Completed&&open.IsLocked&&invoice.Status==InvoiceStatus.Paid&&invoice.PaidAmount==1000,"Append preserves original, Visit and financial data");
            var race1=Append(linked.Id,new(4,"first",Guid.NewGuid(),true));var race2=Append(linked.Id,new(4,"second",Guid.NewGuid(),true));var races=await Task.WhenAll(race1,race2);check(races.Count(r=>r.StatusCode==HttpStatusCode.OK)==1&&races.Count(r=>r.StatusCode==HttpStatusCode.Conflict)==1,"Concurrent writes have one winner");foreach(var r in races)r.Dispose();check(await db.DentalImageClinicalAddenda.CountAsync(a=>a.DentalImageId==linked.Id)==2,"Concurrent append creates exactly one additional row");
            users[RoleCodes.Dentist].IsActive=false;await db.SaveChangesAsync();using(var r=await Append(linked.Id,request))check(r.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized,"Inactive actor cannot replay saved request ("+(int)r.StatusCode+")");
            As(RoleCodes.Admin);patient.IsActive=false;await db.SaveChangesAsync();await Status(client.GetAsync(Url(linked.Id)),HttpStatusCode.NotFound,"Inactive patient concealed");patient.IsActive=true;await db.Entry(linked).ReloadAsync();linked.IsDeleted=true;await db.SaveChangesAsync();await Status(client.GetAsync(Url(linked.Id)),HttpStatusCode.NotFound,"Deleted image concealed");
        } finally {failure.Fail=false;await app.StopAsync();}
    }
}
