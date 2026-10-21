using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Api.RateLimiting;
using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Features.Auth.Validators;
using Dental.Application.Features.Staff;
using Dental.Application.Features.Staff.DTOs;
using Dental.Application.Features.Staff.Services;
using Dental.Application.Features.Staff.Validators;
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

// Isolated synthetic users and fake transport only. Production SMS is never configured by this harness.
internal static class StaffPhoneChangeHttpSmoke
{
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Sms : IStaffPhoneChangeSms
    {
        public bool IsConfigured { get; set; } = true;
        public bool Delivered = true;
        public Dictionary<string,string> Codes = [];
        public Task<bool> SendAsync(string phone,string code,CancellationToken ct) { Codes[phone]=code;return Task.FromResult(Delivered); }
    }
    private sealed class Failure : SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData d,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if (Enabled && d.Context!.ChangeTracker.Entries<AuditLog>().Any(e=>e.State==EntityState.Added&&e.Entity.Action=="MOD_AUTH_STAFF_PHONE_CHANGED"))
                throw new InvalidOperationException("Synthetic required audit failure");
            return base.SavingChangesAsync(d,result,ct);
        }
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options,Action<bool,string> check)
    {
        var clock=new Clock();var sms=new Sms();var failure=new Failure();var dbOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure).Options;
        var b=WebApplication.CreateBuilder(new WebApplicationOptions{Args=[],EnvironmentName="Testing"});b.Logging.ClearProviders();b.WebHost.UseUrls("http://127.0.0.1:0");
        var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));b.Configuration["Jwt:Secret"]=secret;
        b.Services.AddSingleton<TimeProvider>(clock);b.Services.AddSingleton<VietnamClock>();b.Services.AddSingleton<IStaffPhoneChangeSms>(sms);b.Services.AddSingleton<IStaffPhoneChangeProofs,StaffPhoneChangeProofs>();
        b.Services.AddScoped(_=>new DentalDbContext(dbOptions));b.Services.AddScoped<IStaffPhoneChangeRepository,StaffPhoneChangeRepository>();b.Services.AddScoped<StaffPhoneChangeService>();
        b.Services.AddScoped<IPasswordHasher,BCryptPasswordHasher>();b.Services.AddScoped<IUserRepository,UserRepository>();b.Services.AddScoped<IPatientRepository,PatientRepository>();b.Services.AddScoped<IUnitOfWork,UnitOfWork>();b.Services.AddScoped<IAuditLogger,AuditLogger>();b.Services.AddScoped<ITokenService,JwtTokenService>();
        b.Services.AddScoped<AuthService>();b.Services.AddScoped<LoginRequestValidator>();b.Services.AddScoped<StaffService>();b.Services.AddScoped<CreateStaffRequestValidator>();b.Services.AddScoped<StaffQueryRequestValidator>();b.Services.AddScoped<UpdateStaffRequestValidator>();
        b.Services.AddHttpContextAccessor();b.Services.AddScoped<ICurrentUser,CurrentUser>();b.Services.AddControllers().AddApplicationPart(typeof(StaffController).Assembly);
        b.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt=>{jwt.TokenValidationParameters=new TokenValidationParameters{ValidateIssuer=true,ValidIssuer="DentalClinic",ValidateAudience=true,ValidAudience="DentalClinicClient",IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),ClockSkew=TimeSpan.Zero};jwt.Events=new JwtBearerEvents{OnTokenValidated=AccessTokenVersionValidation.ValidateAsync};});
        b.Services.AddAuthorization(o=>o.AddApplicationPolicies());b.Services.AddRateLimiter(o=>{AuthRateLimitPolicies.Configure(o,b.Configuration);StaffPhoneChangeRateLimits.Configure(o);});
        await using var app=b.Build();var ip=0;var fixedIp=false;app.Use(async(c,next)=>{var n=fixedIp?60000:Interlocked.Increment(ref ip);c.Connection.RemoteIpAddress=IPAddress.Parse($"10.2.{n/250}.{n%250+1}");try{await next(c);}catch{c.Response.StatusCode=500;}});app.UseAuthentication();app.UseRateLimiter();app.UseAuthorization();app.MapControllers();await app.StartAsync();
        using var client=new HttpClient{BaseAddress=new Uri(app.Urls.Single())};await using var db=new DentalDbContext(dbOptions);var hasher=new BCryptPasswordHasher();var tokens=new JwtTokenService(b.Configuration);var hash=hasher.Hash("Legacy123");var seq=0;
        var roles=new Dictionary<string,Role>();foreach(var code in new[]{RoleCodes.Admin,RoleCodes.Receptionist,RoleCodes.Dentist,RoleCodes.Assistant,RoleCodes.Patient}){var role=new Role{RoleCode=code,RoleName="Synthetic "+code};roles.Add(code,role);db.Roles.Add(role);}await db.SaveChangesAsync();
        async Task<User> Create(string role=RoleCodes.Dentist,bool verified=true,bool pending=false)
        {var u=new User{Role=roles[role],Phone="09678"+(++seq).ToString("D5"),FullName="Synthetic staff "+seq,PasswordHash=hash,PhoneVerifiedAtUtc=verified?clock.Now.UtcDateTime:null,RequiresPhoneActivation=pending};db.Users.Add(u);await db.SaveChangesAsync();return u;}
        var admin=await Create(RoleCodes.Admin,false);var adminToken=tokens.GenerateAccessToken(admin);var patient=await Create(RoleCodes.Patient,false);var patientToken=tokens.GenerateAccessToken(patient);
        string NewPhone()=>"09679"+(++seq).ToString("D5");
        async Task<HttpResponseMessage> Send(string path,object? data=null,string? token=null,string method="POST")
        {using var request=new HttpRequestMessage(new HttpMethod(method),path);if(data is not null)request.Content=JsonContent.Create(data);if(token is not null)request.Headers.Authorization=new("Bearer",token);return await client.SendAsync(request);}
        const string own="/api/auth/staff-phone-change";
        async Task<StaffPhoneChangePending> Issue(User u,string phone)
        {clock.Now+=TimeSpan.FromSeconds(61);using var r=await Send($"/api/staff/{u.UserId}/phone-change",new StaffPhoneChangeRequest(phone,true),adminToken);check(r.StatusCode==HttpStatusCode.Accepted&&r.Headers.CacheControl?.NoStore==true,"Admin request accepted with NoStore");var text=await r.Content.ReadAsStringAsync();check(!text.Contains("code",StringComparison.OrdinalIgnoreCase)&&!text.Contains("stamp",StringComparison.OrdinalIgnoreCase)&&!text.Contains("password",StringComparison.OrdinalIgnoreCase),"Request response contains no OTP or server binding");return(await r.Content.ReadFromJsonAsync<StaffPhoneChangePending>())!;}
        async Task<HttpStatusCode> Confirm(User u,StaffPhoneChangePending p,string? code=null,string? token=null)
        {using var r=await Send(own+"/confirm",new StaffPhoneChangeConfirmation(p.ChallengeId,code??sms.Codes[p.NewPhone],true),token??tokens.GenerateAccessToken(u));if(r.StatusCode!=HttpStatusCode.Unauthorized)check(r.Headers.CacheControl?.NoStore==true,"Confirmation response is NoStore");return r.StatusCode;}
        async Task<bool> Session(string token){using var r=await Send("/api/auth/me",token:token,method:"GET");return r.IsSuccessStatusCode;}
        var target=await Create();var targetToken=tokens.GenerateAccessToken(target);var initialPhone=target.Phone;
        using(var r=await Send($"/api/staff/{target.UserId}/phone-change",new StaffPhoneChangeRequest(NewPhone(),true)))check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous cannot initiate phone change");
        using(var r=await Send(own,method:"GET"))check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous cannot inspect pending phone");
        foreach(var role in new[]{RoleCodes.Receptionist,RoleCodes.Dentist,RoleCodes.Assistant,RoleCodes.Patient})
        {var u=await Create(role);using var r=await Send($"/api/staff/{target.UserId}/phone-change",new StaffPhoneChangeRequest(NewPhone(),true),tokens.GenerateAccessToken(u));check(r.StatusCode==HttpStatusCode.Forbidden,"Only Admin requests, not "+role);}
        using(var r=await Send(own,token:patientToken,method:"GET"))check(r.StatusCode==HttpStatusCode.Forbidden,"Patient cannot use staff phone APIs");
        using(var r=await Send($"/api/staff/{target.UserId}",new UpdateStaffRequest(target.FullName,NewPhone(),null,null,null,RoleCodes.Dentist),adminToken,"PUT"))check(r.StatusCode==HttpStatusCode.BadRequest&&(await r.Content.ReadAsStringAsync()).Contains("STAFF_PHONE_OTP_REQUIRED"),"PUT profile cannot bypass OTP for verified phone");
        foreach(var phone in new[]{"bad","0967000001\n","0１２３４５６７８９"}){using var r=await Send($"/api/staff/{target.UserId}/phone-change",new StaffPhoneChangeRequest(phone,true),adminToken);check(r.StatusCode==HttpStatusCode.BadRequest,"Malformed new phone rejected");}
        using(var r=await Send($"/api/staff/{target.UserId}/phone-change",new StaffPhoneChangeRequest(NewPhone(),false),adminToken))check(r.StatusCode==HttpStatusCode.BadRequest,"Admin consent required");
        var legacy=await Create(RoleCodes.Assistant,false);var pending=await Create(RoleCodes.Assistant,false,true);var locked=await Create();await db.Users.Where(u=>u.UserId==locked.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.IsActive,false));
        foreach(var u in new[]{pending,locked,patient}){using var r=await Send($"/api/staff/{u.UserId}/phone-change",new StaffPhoneChangeRequest(NewPhone(),true),adminToken);check(!r.IsSuccessStatusCode,"Pending, locked or Patient cannot use staff phone change");}
        foreach(var role in new[]{RoleCodes.Admin,RoleCodes.Receptionist,RoleCodes.Dentist,RoleCodes.Assistant})
        {
            var u=await Create(role,false);var oldPhone=u.Phone;var oldToken=tokens.GenerateAccessToken(u);
            using(var r=await Send($"/api/staff/{u.UserId}",new UpdateStaffRequest(u.FullName,NewPhone(),null,null,null,role),adminToken,"PUT"))
                check(r.StatusCode==HttpStatusCode.BadRequest&&(await r.Content.ReadAsStringAsync()).Contains("STAFF_PHONE_OTP_REQUIRED"),"Legacy "+role+" cannot bypass OTP through PUT");
            var p=await Issue(u,NewPhone());await db.Entry(u).ReloadAsync();
            check(u.Phone==oldPhone&&u.PhoneVerifiedAtUtc is null&&u.TokenVersion==0&&u.PasswordHash==hash,"Legacy pending preserves old phone, NULL proof, hash and version");
            check(await Session(oldToken),"Legacy old session valid while awaiting OTP");
            using(var r=await Send(own,token:oldToken,method:"GET"))check(r.IsSuccessStatusCode&&(await r.Content.ReadAsStringAsync()).Contains(p.NewPhone),"Legacy owner reads own new-phone request");
            check(await Confirm(u,p,token:oldToken)==HttpStatusCode.OK,"Legacy "+role+" confirms only new phone OTP");
            await db.Entry(u).ReloadAsync();check(u.Phone==p.NewPhone&&u.PhoneVerifiedAtUtc.HasValue&&u.TokenVersion==1&&u.PasswordHash==hash,"Only successful legacy change stores real verification and revokes version");
            check(!await Session(oldToken),"Legacy prior sessions revoked after successful commit");
            using(var r=await Send("/api/auth/login",new LoginRequest(u.Phone,"Legacy123")))check(r.IsSuccessStatusCode,"Legacy new number logs in with same password");
            using(var r=await Send("/api/auth/login",new LoginRequest(oldPhone,"Legacy123")))check(r.StatusCode==HttpStatusCode.Unauthorized,"Legacy old number no longer logs in");
            check(await db.AuditLogs.AnyAsync(a=>a.EntityId==u.UserId&&a.Action=="MOD_AUTH_STAFF_PHONE_CHANGED"&&a.Detail!.Contains("DL-333")),"Legacy change audit refers to approved DL333");
        }
        var legacyRollback=await Create(RoleCodes.Assistant,false);var legacyRollbackPhone=legacyRollback.Phone;var legacyRollbackToken=tokens.GenerateAccessToken(legacyRollback);var legacyRollbackProof=await Issue(legacyRollback,NewPhone());
        failure.Enabled=true;check(await Confirm(legacyRollback,legacyRollbackProof,token:legacyRollbackToken)==HttpStatusCode.InternalServerError,"Legacy audit failure aborts number change");failure.Enabled=false;
        await db.Entry(legacyRollback).ReloadAsync();check(legacyRollback.Phone==legacyRollbackPhone&&legacyRollback.PhoneVerifiedAtUtc is null&&legacyRollback.TokenVersion==0&&legacyRollback.PasswordHash==hash,"Legacy audit rollback leaves no fake verification or credential change");check(await Session(legacyRollbackToken),"Legacy original session survives failed transaction");
        var legacyExpire=await Issue(legacyRollback,NewPhone());clock.Now+=TimeSpan.FromMinutes(5);check(await Confirm(legacyRollback,legacyExpire,token:legacyRollbackToken)==HttpStatusCode.BadRequest,"Legacy new-phone OTP expires without account change");
        await db.Entry(legacyRollback).ReloadAsync();check(legacyRollback.PhoneVerifiedAtUtc is null&&legacyRollback.Phone==legacyRollbackPhone,"Legacy expiry leaves old phone unverified");
        using(var r=await Send($"/api/staff/{legacy.UserId}",new UpdateStaffRequest("Synthetic profile update",legacy.Phone,null,null,null,RoleCodes.Assistant),adminToken,"PUT"))check(r.IsSuccessStatusCode,"Legacy profile update retaining phone requires no OTP");
        sms.IsConfigured=false;using(var r=await Send($"/api/staff/{target.UserId}/phone-change",new StaffPhoneChangeRequest(NewPhone(),true),adminToken))check(r.StatusCode==HttpStatusCode.ServiceUnavailable,"Missing SMS provider fails closed");sms.IsConfigured=true;
        sms.Delivered=false;using(var r=await Send($"/api/staff/{target.UserId}/phone-change",new StaffPhoneChangeRequest(NewPhone(),true),adminToken))check(r.StatusCode==HttpStatusCode.ServiceUnavailable,"Failed SMS delivery fails closed");sms.Delivered=true;
        var next=NewPhone();var proof=await Issue(target,next);
        using(var r=await Send(own,token:targetToken,method:"GET")){check(r.IsSuccessStatusCode&&r.Headers.CacheControl?.NoStore==true,"Owner can read own pending request");var body=await r.Content.ReadAsStringAsync();check(body.Contains(next)&&!body.Contains(sms.Codes[next]),"Only pending new phone, not OTP, returned to owner");}
        var other=await Create();using(var r=await Send(own,token:tokens.GenerateAccessToken(other),method:"GET"))check(!(await r.Content.ReadAsStringAsync()).Contains(next),"Another staff member cannot read target pending phone");
        check(await Confirm(other,proof)==HttpStatusCode.BadRequest,"Another staff member cannot confirm target proof");
        using(var r=await Send($"/api/staff/{target.UserId}/phone-change",new StaffPhoneChangeRequest(NewPhone(),true),adminToken))check(r.StatusCode==HttpStatusCode.TooManyRequests,"Target resend cooldown cannot be bypassed with another phone");
        await db.Entry(target).ReloadAsync();check(target.Phone==initialPhone&&target.PasswordHash==hash&&target.TokenVersion==0,"Pending leaves phone, password and version unchanged");check(await Session(targetToken),"Old session remains valid while pending");
        using(var r=await Send(own+"/confirm",new StaffPhoneChangeConfirmation(proof.ChallengeId,sms.Codes[next],false),targetToken))check(r.StatusCode==HttpStatusCode.BadRequest,"Owner consent required before code consumption");
        using(var r=await Send(own+"/confirm",new StaffPhoneChangeConfirmation(proof.ChallengeId,sms.Codes[next]+"\n",true),targetToken))check(r.StatusCode==HttpStatusCode.BadRequest,"Malformed OTP does not consume proof");
        var race=await Task.WhenAll(Confirm(target,proof,token:targetToken),Confirm(target,proof,token:targetToken));check(race.Count(s=>s==HttpStatusCode.OK)==1&&race.All(s=>s is HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized),"Concurrent confirmation has one successful change");
        await db.Entry(target).ReloadAsync();check(target.Phone==next&&target.PasswordHash==hash&&target.TokenVersion==1&&target.PhoneVerifiedAtUtc.HasValue,"Commit changes number and real verification, revokes version, preserves hash");check(!await Session(targetToken),"All prior target sessions revoked after commit");
        using(var r=await Send("/api/auth/login",new LoginRequest(initialPhone,"Legacy123")))check(r.StatusCode==HttpStatusCode.Unauthorized,"Old number no longer logs in");
        using(var r=await Send("/api/auth/login",new LoginRequest(next,"Legacy123")))check(r.IsSuccessStatusCode,"New number logs in with unchanged password");
        using(var r=await Send("/api/auth/login",new LoginRequest(next,next)))check(r.StatusCode==HttpStatusCode.Unauthorized,"New number is not used as a replacement password");
        check(await Confirm(target,proof)==HttpStatusCode.BadRequest,"Consumed phone proof cannot replay with a new session");
        var wrongTarget=await Create();var wrong=await Issue(wrongTarget,NewPhone());var right=sms.Codes[wrong.NewPhone];for(var n=0;n<5;n++)check(await Confirm(wrongTarget,wrong,right=="000000"?"111111":"000000")==HttpStatusCode.BadRequest,"Wrong OTP rejected");check(await Confirm(wrongTarget,wrong)==HttpStatusCode.BadRequest,"Fifth wrong code locks right code too");
        var expired=await Issue(wrongTarget,NewPhone());clock.Now+=TimeSpan.FromMinutes(5);check(await Confirm(wrongTarget,expired)==HttpStatusCode.BadRequest,"Phone change expires exactly at five minutes");
        var replaced=await Issue(wrongTarget,NewPhone());var replacement=await Issue(wrongTarget,NewPhone());check(await Confirm(wrongTarget,replaced)==HttpStatusCode.BadRequest,"Resend invalidates earlier OTP");check(await Confirm(wrongTarget,replacement)==HttpStatusCode.OK,"Latest request remains usable");
        var failed=await Create();var failedProof=await Issue(failed,NewPhone());failure.Enabled=true;check(await Confirm(failed,failedProof)==HttpStatusCode.InternalServerError,"Required audit failure aborts change");failure.Enabled=false;
        await db.Entry(failed).ReloadAsync();check(failed.Phone!=failedProof.NewPhone&&failed.TokenVersion==0&&failed.PasswordHash==hash,"Audit failure rolls back number, version and password");check(await Confirm(failed,failedProof)==HttpStatusCode.BadRequest,"Failed transaction proof cannot replay");
        foreach(var kind in new[]{"password","role","lock","version"})
        {var u=await Create();var p=await Issue(u,NewPhone());if(kind=="password")await db.Users.Where(x=>x.UserId==u.UserId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.PasswordHash,"synthetic-other-hash"));else if(kind=="role")await db.Users.Where(x=>x.UserId==u.UserId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RoleId,roles[RoleCodes.Assistant].RoleId));else if(kind=="lock")await db.Users.Where(x=>x.UserId==u.UserId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false));else await db.Users.Where(x=>x.UserId==u.UserId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.TokenVersion,1));using var r=await Send(own+"/confirm",new StaffPhoneChangeConfirmation(p.ChallengeId,sms.Codes[p.NewPhone],true),tokens.GenerateAccessToken(u));check(!r.IsSuccessStatusCode,"Changed target "+kind+" invalidates pending binding");check(!await db.Users.AnyAsync(x=>x.UserId==u.UserId&&x.Phone==p.NewPhone),"Stale binding never changes phone");}
        var stale=await Create();var staleProof=await Issue(stale,NewPhone());await db.Users.Where(u=>u.UserId==admin.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.RoleId,roles[RoleCodes.Dentist].RoleId));
        using(var r=await Send($"/api/staff/{stale.UserId}/phone-change",new StaffPhoneChangeRequest(NewPhone(),true),adminToken))check(r.StatusCode==HttpStatusCode.Forbidden,"Stale Admin claim cannot request phone OTP");check(await Confirm(stale,staleProof)==HttpStatusCode.BadRequest,"Demoted requesting Admin invalidates old request");await db.Users.Where(u=>u.UserId==admin.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.RoleId,roles[RoleCodes.Admin].RoleId));
        var lockedActorProof=await Issue(stale,NewPhone());await db.Users.Where(u=>u.UserId==admin.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.IsActive,false));check(await Confirm(stale,lockedActorProof)==HttpStatusCode.BadRequest,"Locked requesting Admin invalidates pending request");await db.Users.Where(u=>u.UserId==admin.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.IsActive,true));
        var occupiedPhone=NewPhone();var occupiedProof=await Issue(stale,occupiedPhone);var holder=await Create();await db.Users.Where(u=>u.UserId==holder.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.Phone,occupiedPhone));check(await Confirm(stale,occupiedProof)==HttpStatusCode.Conflict,"Phone taken while pending rejected at commit");
        var a=await Create();var z=await Create();var sharedPhone=NewPhone();var pa=await Issue(a,sharedPhone);var aCode=sms.Codes[sharedPhone];var pz=await Issue(z,sharedPhone);var shared=await Task.WhenAll(Confirm(a,pa,aCode),Confirm(z,pz));check(shared.Count(s=>s==HttpStatusCode.OK)==1&&shared.Count(s=>s==HttpStatusCode.Conflict)==1,"Two users racing for same phone yield one winner and one conflict");check(await db.Users.CountAsync(u=>u.Phone==sharedPhone)==1,"Unique login phone retained under concurrency");
        var selfAdmin=await Create(RoleCodes.Admin);var oldAdminToken=tokens.GenerateAccessToken(selfAdmin);clock.Now+=TimeSpan.FromSeconds(61);var selfPhone=NewPhone();using(var r=await Send($"/api/staff/{selfAdmin.UserId}/phone-change",new StaffPhoneChangeRequest(selfPhone,true),oldAdminToken)){check(r.StatusCode==HttpStatusCode.Accepted,"Verified Admin can request own new phone");var p=(await r.Content.ReadFromJsonAsync<StaffPhoneChangePending>())!;check(await Confirm(selfAdmin,p,token:oldAdminToken)==HttpStatusCode.OK,"Admin confirms own number without losing access before commit");}check(!await Session(oldAdminToken),"Self Admin old sessions revoked only on completion");
        fixedIp=true;for(var n=0;n<10;n++){using var r=await Send(own+"/confirm",new StaffPhoneChangeConfirmation(Guid.NewGuid(),"123456",true),tokens.GenerateAccessToken(stale));check(r.StatusCode==HttpStatusCode.BadRequest,"Bounded confirmation attempt allowed before IP limit");}using(var r=await Send(own+"/confirm",new StaffPhoneChangeConfirmation(Guid.NewGuid(),"123456",true),tokens.GenerateAccessToken(stale)))check(r.StatusCode==HttpStatusCode.TooManyRequests&&r.Headers.CacheControl?.NoStore==true,"Confirmation IP limiter rejects with NoStore");fixedIp=false;
        check(await db.AuditLogs.Where(a=>a.Action.StartsWith("MOD_AUTH_STAFF_PHONE_")).AllAsync(a=>a.Detail!=null&&!a.Detail.Contains("0967")&&!a.Detail.Contains("Legacy123")&&!a.Detail.Contains("Stamp")&&!a.Detail.Contains("Code")),"Phone request/completion audits exclude phone, OTP, hash and binding");
        await db.Entry(legacy).ReloadAsync();check(legacy.PhoneVerifiedAtUtc is null&&!legacy.RequiresPhoneActivation&&legacy.TokenVersion==0,"Legacy account verification is not fabricated");
        await app.StopAsync();
    }
}
