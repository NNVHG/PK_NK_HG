using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Api.RateLimiting;
using Dental.Application.Features.MOD_IMG;
using Dental.Application.Features.MOD_PAT;
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

internal static class PatientAccountLinkHttpSmoke
{
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Fail && data.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action == "MOD_PAT_FAMILY_ACCOUNT_LINKED")) throw new InvalidOperationException("Synthetic audit failure");
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
        builder.Services.AddScoped<IPatientAccountLinkRepository, PatientAccountLinkRepository>(); builder.Services.AddScoped<PatientAccountLinkService>();
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
        { var user = new User { Role = await db.Roles.FirstOrDefaultAsync(r=>r.RoleCode==role) ?? new Role {RoleCode=role,RoleName="Synthetic "+role}, FullName = "Synthetic image account", Phone = "096500000" + users.Count, PasswordHash = "synthetic-not-used" }; users[role] = user; db.Users.Add(user); }
        await db.SaveChangesAsync();
        var target=users[RoleCodes.Patient];
        var other=new User{Role=target.Role,Phone="0965000009",FullName="Synthetic other family",PasswordHash="synthetic-not-used"};db.Users.Add(other);await db.SaveChangesAsync();
        async Task<Patient> Patient(string name,string? phone=null,int? owner=null,bool active=true){var p=new Patient{FullName=name,DateOfBirth=new DateOnly(2000,1,1),Phone=phone??target.Phone,UserId=owner,IsActive=active};db.Patients.Add(p);await db.SaveChangesAsync();return p;}
        Patient one=await Patient("Synthetic unlinked one"),two=await Patient("Synthetic unlinked two"),stale=await Patient("Synthetic stale"),rollback=await Patient("Synthetic rollback"),race=await Patient("Synthetic race"),inactive=await Patient("Synthetic inactive",active:false),missing=await Patient("Synthetic no account","0965000099"),staff=await Patient("Synthetic staff phone",users[RoleCodes.Dentist].Phone);
        async Task<HttpResponseMessage> Send(string? role,int id,object? body=null,string method="GET"){
            var req=new HttpRequestMessage(new HttpMethod(method),$"/api/patients/{id}/family-account");
            if(role is not null)req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",tokens.GenerateAccessToken(users[role]));
            if(body is not null)req.Content=JsonContent.Create(body);
            return await client.SendAsync(req);
        }
        async Task<PatientAccountLinkContext> Read(int id,string role=RoleCodes.Receptionist){using var res=await Send(role,id);check(res.StatusCode==HttpStatusCode.OK,"Allowed context "+role);check(res.Headers.CacheControl?.NoStore==true,"Context NoStore");return (await res.Content.ReadFromJsonAsync<PatientAccountLinkContext>())!;}
        async Task<int?> Owner(int id){await using var d=new DentalDbContext(dbOptions);return await d.Patients.Where(p=>p.PatientId==id).Select(p=>p.UserId).SingleAsync();}
        async Task<int> Audits(int id){await using var d=new DentalDbContext(dbOptions);return await d.AuditLogs.CountAsync(a=>a.EntityId==id&&a.Action=="MOD_PAT_FAMILY_ACCOUNT_LINKED");}
        object Body(PatientAccountLinkContext c)=>new{accountUserId=c.Account!.UserId,reviewStamp=c.ReviewStamp,confirmed=true};
        try{
            foreach(var role in new string?[]{null,RoleCodes.Dentist,RoleCodes.Assistant,RoleCodes.Patient}){
                var expected=role is null?HttpStatusCode.Unauthorized:HttpStatusCode.Forbidden;
                using var get=await Send(role,one.PatientId);using var post=await Send(role,one.PatientId,new{accountUserId=target.UserId,reviewStamp=new string('A',64),confirmed=true},"POST");
                check(get.StatusCode==expected&&post.StatusCode==expected,"Read/write policy "+(role??"anonymous"));
            }
            var ctx=await Read(one.PatientId);check(ctx.Account?.UserId==target.UserId&&ctx.ReviewStamp?.Length==64&&!ctx.IsLinked,"Single matching active Patient account for review");
            check(await Owner(one.PatientId)==null&&await Audits(one.PatientId)==0,"Preview never auto-links matching phone");
            using(var raw=await Send(RoleCodes.Admin,one.PatientId)){var json=await raw.Content.ReadAsStringAsync();check(!json.Contains("password",StringComparison.OrdinalIgnoreCase)&&!json.Contains("tokenVersion")&&!json.Contains("citizenId")&&!json.Contains("profiles"),"Context has no credentials, identity hash or other family profiles");}
            using(var no=await Send(RoleCodes.Admin,0))check(no.StatusCode==HttpStatusCode.BadRequest,"Invalid patient id rejected");
            using(var no=await Send(RoleCodes.Admin,int.MaxValue))check(no.StatusCode==HttpStatusCode.NotFound,"Missing patient hidden");
            using(var no=await Send(RoleCodes.Admin,inactive.PatientId))check(no.StatusCode==HttpStatusCode.NotFound,"Inactive patient hidden");
            using(var no=await Send(RoleCodes.Admin,inactive.PatientId,Body(ctx),"POST"))check(no.StatusCode==HttpStatusCode.NotFound&&await Owner(inactive.PatientId)==null,"Inactive patient cannot be linked");
            foreach(var p in new[]{missing,staff}){var c=await Read(p.PatientId);check(c.Account==null&&c.ReviewStamp==null,"No eligible account returned");using var bad=await Send(RoleCodes.Admin,p.PatientId,Body(ctx),"POST");check(bad.StatusCode==HttpStatusCode.Conflict&&await Owner(p.PatientId)==null,"No account or staff account cannot be linked");}
            foreach(var body in new object[]{new{accountUserId=0,reviewStamp=ctx.ReviewStamp,confirmed=true},new{accountUserId=target.UserId,reviewStamp=ctx.ReviewStamp,confirmed=false},new{accountUserId=target.UserId,reviewStamp="bad",confirmed=true}}){using var res=await Send(RoleCodes.Admin,one.PatientId,body,"POST");check(res.StatusCode==HttpStatusCode.BadRequest,"Invalid request rejected before write");}
            using(var wrong=await Send(RoleCodes.Admin,one.PatientId,new{accountUserId=other.UserId,reviewStamp=ctx.ReviewStamp,confirmed=true},"POST"))check(wrong.StatusCode==HttpStatusCode.Conflict,"Cannot substitute another family account");
            // A phone match never authorizes portal access until the explicit link succeeds.
            async Task<HttpStatusCode> Images(User user,int patient){using var req=new HttpRequestMessage(HttpMethod.Get,$"/api/dental-images?patientId={patient}");req.Headers.Authorization=new("Bearer",tokens.GenerateAccessToken(user));using var res=await client.SendAsync(req);return res.StatusCode;}
            check(await Images(target,one.PatientId)==HttpStatusCode.NotFound,"Matching phone alone cannot read images");
            var original=await db.Patients.AsNoTracking().SingleAsync(p=>p.PatientId==one.PatientId);
            using(var ok=await Send(RoleCodes.Admin,one.PatientId,new{accountUserId=target.UserId,reviewStamp=ctx.ReviewStamp,confirmed=true,patientName="injected",userId=other.UserId,isActive=false},"POST")){check(ok.StatusCode==HttpStatusCode.OK&&ok.Headers.CacheControl?.NoStore==true,"Admin links with NoStore result");var saved=await ok.Content.ReadFromJsonAsync<PatientAccountLinked>();check(saved==new PatientAccountLinked(one.PatientId,target.UserId),"Result identifies exact patient and account");}
            check(await Owner(one.PatientId)==target.UserId&&await Audits(one.PatientId)==1,"Owner and single audit committed together");
            await db.Entry(one).ReloadAsync();check(one.FullName==original.FullName&&one.Phone==original.Phone&&one.IsActive==original.IsActive&&one.DateOfBirth==original.DateOfBirth&&one.CitizenIdVersion==original.CitizenIdVersion,"Extra JSON cannot edit patient identity or state");
            var audit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityId==one.PatientId&&a.Action=="MOD_PAT_FAMILY_ACCOUNT_LINKED");check(audit.UserId==users[RoleCodes.Admin].UserId&&!audit.Detail!.Contains(one.Phone)&&!audit.Detail.Contains(one.FullName)&&!audit.Detail.Contains(ctx.ReviewStamp!),"Audit records actor/target IDs without demographics or review stamp");
            check(await Images(target,one.PatientId)==HttpStatusCode.OK&&await Images(other,one.PatientId)==HttpStatusCode.NotFound,"Linked family can read images; other family cannot");
            var linked=await Read(one.PatientId);check(linked.IsLinked&&linked.Account==null&&linked.ReviewStamp==null,"Already-linked context has no relink target");
            using(var repeat=await Send(RoleCodes.Receptionist,one.PatientId,Body(ctx),"POST"))check(repeat.StatusCode==HttpStatusCode.Conflict&&await Audits(one.PatientId)==1,"Repeat cannot transfer ownership or duplicate audit");
            await using(var familyDb=new DentalDbContext(dbOptions)){var profiles=await new PatientRepository(familyDb).GetActiveByUserIdAsync(target.UserId,default);check(profiles.Any(p=>p.PatientId==one.PatientId),"Linked profile appears in existing family selector repository");}
            var c2=await Read(two.PatientId);using(var ok=await Send(RoleCodes.Receptionist,two.PatientId,Body(c2),"POST"))check(ok.StatusCode==HttpStatusCode.OK&&await Owner(two.PatientId)==target.UserId,"Receptionist links another family member to same User");
            // Any visible review identity changing invalidates the previous confirmation.
            foreach(var change in new[]{"patient-name","patient-birth","patient-phone","citizen-version","account-name","account-birth","account-phone","account-inactive","account-role"}){
                var c=await Read(stale.PatientId);var oldPatientName=stale.FullName;var oldPhone=stale.Phone;var oldName=target.FullName;var oldTargetPhone=target.Phone;var oldRole=target.RoleId;var oldBirth=target.DateOfBirth;
                if(change=="patient-name")stale.FullName+=" changed";if(change=="patient-birth")stale.DateOfBirth=stale.DateOfBirth.AddDays(1);if(change=="patient-phone")stale.Phone="0965000098";if(change=="citizen-version")stale.CitizenIdVersion++;
                if(change=="account-name")target.FullName+=" changed";if(change=="account-birth")target.DateOfBirth=new DateOnly(1980,2,2);if(change=="account-phone")target.Phone="0965000097";if(change=="account-inactive")target.IsActive=false;if(change=="account-role")target.RoleId=users[RoleCodes.Dentist].RoleId;
                await db.SaveChangesAsync();using(var changed=await Send(RoleCodes.Admin,stale.PatientId,Body(c),"POST"))check(changed.StatusCode==HttpStatusCode.Conflict&&await Owner(stale.PatientId)==null,"Stale review blocked: "+change);
                stale.FullName=oldPatientName;stale.Phone=oldPhone;stale.DateOfBirth=new DateOnly(2000,1,1);target.FullName=oldName;target.Phone=oldTargetPhone;target.RoleId=oldRole;target.DateOfBirth=oldBirth;target.IsActive=true;await db.SaveChangesAsync();
            }
            var ca=await Read(stale.PatientId);var receptionist=users[RoleCodes.Receptionist];receptionist.IsActive=false;await db.SaveChangesAsync();using(var denied=await Send(RoleCodes.Receptionist,stale.PatientId,Body(ca),"POST"))check(denied.StatusCode==HttpStatusCode.Forbidden,"Inactive actor cannot write with old token");receptionist.IsActive=true;var roleId=receptionist.RoleId;receptionist.RoleId=users[RoleCodes.Assistant].RoleId;await db.SaveChangesAsync();using(var denied=await Send(RoleCodes.Receptionist,stale.PatientId))check(denied.StatusCode==HttpStatusCode.Forbidden,"Actual DB role blocks stale JWT claim");receptionist.RoleId=roleId;await db.SaveChangesAsync();
            var cr=await Read(rollback.PatientId);failure.Fail=true;using(var failed=await Send(RoleCodes.Admin,rollback.PatientId,Body(cr),"POST"))check(failed.StatusCode==HttpStatusCode.InternalServerError,"Synthetic audit failure propagated");failure.Fail=false;check(await Owner(rollback.PatientId)==null&&await Audits(rollback.PatientId)==0,"Audit failure rolls back owner and log");
            var cc=await Read(race.PatientId);var attempts=await Task.WhenAll(Send(RoleCodes.Admin,race.PatientId,Body(cc),"POST"),Send(RoleCodes.Receptionist,race.PatientId,Body(cc),"POST"));check(attempts.Count(x=>x.StatusCode==HttpStatusCode.OK)==1&&attempts.Count(x=>x.StatusCode==HttpStatusCode.Conflict)==1,"Concurrent confirmations have one winner");foreach(var res in attempts)res.Dispose();check(await Owner(race.PatientId)==target.UserId&&await Audits(race.PatientId)==1,"Race has one owner and one audit");
        }finally{await app.StopAsync();if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
