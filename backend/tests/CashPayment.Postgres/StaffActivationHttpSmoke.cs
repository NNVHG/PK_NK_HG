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
using Dental.Application.Features.MOD_AUTH;
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

internal static class StaffActivationHttpSmoke
{
    private sealed class Clock:TimeProvider {public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Sms:IPatientActivationDelivery
    {public bool IsConfigured{get;set;}=true;public Dictionary<string,ActivationDispatch> Codes=[];public bool Enqueue(ActivationDispatch d){Codes[d.Phone]=d;return true;}}
    private sealed class Failure:SaveChangesInterceptor
    {
        public bool Create,Activate;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData d,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(d.Context!.ChangeTracker.Entries<AuditLog>().Any(e=>e.State==EntityState.Added&&(Create&&e.Entity.Action==AuditActions.Create||Activate&&e.Entity.Action=="MOD_AUTH_STAFF_ACTIVATED")))throw new InvalidOperationException("Synthetic required audit failure");
            return base.SavingChangesAsync(d,result,ct);
        }
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options,Action<bool,string> check)
    {
        var clock=new Clock();var sms=new Sms();var failure=new Failure();var dbOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure).Options;
        var b=WebApplication.CreateBuilder(new WebApplicationOptions{Args=[],EnvironmentName="Testing"});b.Logging.ClearProviders();b.WebHost.UseUrls("http://127.0.0.1:0");
        var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));b.Configuration["Jwt:Secret"]=secret;
        b.Services.AddSingleton<TimeProvider>(clock);b.Services.AddSingleton<VietnamClock>();b.Services.AddSingleton<IPatientActivationDelivery>(sms);b.Services.AddSingleton<IPatientActivationProofs,PatientActivationProofs>();
        b.Services.AddSingleton<IKeyedIdentityHasher>(new KeyedIdentityHasher(new byte[32])); // Explicit synthetic test key, never a production fallback.
        b.Services.AddScoped(_=>new DentalDbContext(dbOptions));b.Services.AddScoped<IPatientActivationRepository,PatientActivationRepository>();b.Services.AddScoped<PatientActivationService>();
        b.Services.AddScoped<IPasswordHasher,BCryptPasswordHasher>();b.Services.AddScoped<IUserRepository,UserRepository>();b.Services.AddScoped<IPatientRepository,PatientRepository>();b.Services.AddScoped<IUnitOfWork,UnitOfWork>();b.Services.AddScoped<IAuditLogger,AuditLogger>();b.Services.AddScoped<ITokenService,JwtTokenService>();
        b.Services.AddScoped<AuthService>();b.Services.AddScoped<LoginRequestValidator>();b.Services.AddScoped<StaffService>();b.Services.AddScoped<CreateStaffRequestValidator>();b.Services.AddScoped<StaffQueryRequestValidator>();b.Services.AddScoped<UpdateStaffRequestValidator>();
        b.Services.AddHttpContextAccessor();b.Services.AddScoped<ICurrentUser,CurrentUser>();b.Services.AddControllers().AddApplicationPart(typeof(StaffController).Assembly);
        b.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt=>{jwt.TokenValidationParameters=new TokenValidationParameters{ValidateIssuer=true,ValidIssuer="DentalClinic",ValidateAudience=true,ValidAudience="DentalClinicClient",IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),ClockSkew=TimeSpan.Zero};jwt.Events=new JwtBearerEvents{OnTokenValidated=AccessTokenVersionValidation.ValidateAsync};});
        b.Services.AddAuthorization(o=>o.AddApplicationPolicies());b.Services.AddRateLimiter(o=>{AuthRateLimitPolicies.Configure(o,b.Configuration);PatientActivationRateLimits.Configure(o);});
        await using var app=b.Build();var ip=0;app.Use(async(c,next)=>{var n=Interlocked.Increment(ref ip);c.Connection.RemoteIpAddress=IPAddress.Parse($"10.1.{n/250}.{n%250+1}");try{await next(c);}catch{c.Response.StatusCode=500;}});app.UseAuthentication();app.UseRateLimiter();app.UseAuthorization();app.MapControllers();await app.StartAsync();
        using var client=new HttpClient{BaseAddress=new Uri(app.Urls.Single())};await using var db=new DentalDbContext(dbOptions);var hasher=new BCryptPasswordHasher();var tokens=new JwtTokenService(b.Configuration);var roles=new Dictionary<string,Role>();var legacy=new Dictionary<string,User>();
        var hash=hasher.Hash("Legacy123");var seq=0;
        foreach(var code in new[]{RoleCodes.Admin,RoleCodes.Receptionist,RoleCodes.Dentist,RoleCodes.Assistant,RoleCodes.Patient}){var role=new Role{RoleCode=code,RoleName="Synthetic "+code};roles.Add(code,role);var u=new User{Role=role,Phone="096740"+(++seq).ToString("D4"),FullName="Synthetic legacy "+code,PasswordHash=hash};legacy.Add(code,u);db.Users.Add(u);}await db.SaveChangesAsync();
        var admin=legacy[RoleCodes.Admin];var adminToken=tokens.GenerateAccessToken(admin);var oldTokens=legacy.Values.ToDictionary(u=>u.UserId,u=>tokens.GenerateAccessToken(u));
        async Task<HttpResponseMessage> Send(string path,object? data=null,string? token=null,string method="POST"){using var req=new HttpRequestMessage(new HttpMethod(method),path);if(data is not null)req.Content=JsonContent.Create(data);if(token is not null)req.Headers.Authorization=new("Bearer",token);return await client.SendAsync(req);}
        async Task<User> Create(string role)
        {
            var phone="09675"+(++seq).ToString("D5");using var r=await Send("/api/staff",new{fullName="Synthetic new staff "+seq,phone,roleCode=role},adminToken);
            check(r.StatusCode==HttpStatusCode.Created&&r.Headers.CacheControl?.NoStore==true,"Admin creates new staff with omitted password and NoStore");var data=(await r.Content.ReadFromJsonAsync<StaffAccountResponse>())!;
            check(data.RequiresPhoneActivation&&data.RoleCode==role,"Staff DTO reports pending first OTP");var text=await r.Content.ReadAsStringAsync();check(!text.Contains("password",StringComparison.OrdinalIgnoreCase)&&!text.Contains("token",StringComparison.OrdinalIgnoreCase),"Creation response exposes no credentials or proof");
            var u=await db.Users.AsNoTracking().Include(u=>u.Role).SingleAsync(u=>u.UserId==data.UserId);check(hasher.Verify(phone,u.PasswordHash)&&u.RequiresPhoneActivation&&u.PhoneVerifiedAtUtc is null&&u.TokenVersion==0,"Server stores bcrypt default phone and real pending state");return u;
        }
        const string root="/api/auth/activation/";
        async Task<ActivationChallenge> Issue(User u,string? password=null){clock.Now+=TimeSpan.FromSeconds(61);using var r=await Send(root+"request",new ActivationRequest(u.Phone,password??u.Phone));check(r.StatusCode==HttpStatusCode.Accepted&&r.Headers.CacheControl?.NoStore==true,"Staff activation request accepted and NoStore");return(await r.Content.ReadFromJsonAsync<ActivationChallenge>())!;}
        async Task<ActivationGrant> Grant(User u){var c=await Issue(u);using var r=await Send(root+"verify",new ActivationVerification(c.ChallengeId,sms.Codes[u.Phone].Code));check(r.StatusCode==HttpStatusCode.OK&&r.Headers.CacheControl?.NoStore==true,"Correct staff OTP returns completion proof with NoStore");return(await r.Content.ReadFromJsonAsync<ActivationGrant>())!;}
        async Task<HttpStatusCode> Finish(ActivationGrant g,bool keep=true){using var r=await Send(root+"complete",new ActivationCompletion(g.CompletionToken,keep,keep?null:"Selected123",true));return r.StatusCode;}
        async Task<HttpStatusCode> Verify(ActivationChallenge c,string code){using var r=await Send(root+"verify",new ActivationVerification(c.ChallengeId,code));return r.StatusCode;}
        foreach(var u in legacy.Values){using var r=await Send("/api/auth/login",new LoginRequest(u.Phone,"Legacy123"));check(r.StatusCode==HttpStatusCode.OK,"Legacy "+u.Role.RoleCode+" logs in without new OTP");using var me=await Send("/api/auth/me",token:oldTokens[u.UserId],method:"GET");check(me.IsSuccessStatusCode,"Legacy session still authorizes");var count=sms.Codes.Count;await Issue(u,"Legacy123");check(sms.Codes.Count==count,"Legacy role with no flag never receives activation SMS");}
        using(var r=await Send("/api/staff",new{fullName="Synthetic",phone="0967999999",roleCode=RoleCodes.Dentist}))check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous cannot create staff");
        foreach(var u in legacy.Values.Where(u=>u.Role.RoleCode!=RoleCodes.Admin)){using var r=await Send("/api/staff",new{fullName="Synthetic",phone="0967999999",roleCode=RoleCodes.Dentist},oldTokens[u.UserId]);check(r.StatusCode==HttpStatusCode.Forbidden,"Non-admin cannot create staff: "+u.Role.RoleCode);}
        using(var r=await Send("/api/staff",new{fullName="Synthetic",phone="0967999999",password="Chosen123",roleCode=RoleCodes.Dentist},adminToken))check(r.StatusCode==HttpStatusCode.BadRequest,"Old clients selecting initial password receive explicit validation failure");
        var newAdmin=await Create(RoleCodes.Admin);
        using(var r=await Send($"/api/staff/{admin.UserId}",new UpdateStaffRequest(admin.FullName,admin.Phone,null,null,null,RoleCodes.Dentist),adminToken,"PUT"))check(r.StatusCode==HttpStatusCode.Conflict,"Pending new Admin does not permit removal of last usable Admin");
        using(var r=await Send($"/api/staff/{newAdmin.UserId}/lock",token:adminToken))check(r.IsSuccessStatusCode,"Pending Admin can be locked while usable Admin remains");var countBefore=sms.Codes.Count;await Issue(newAdmin);check(sms.Codes.Count==countBefore,"Locked pending Admin never receives SMS");using(var r=await Send($"/api/staff/{newAdmin.UserId}/unlock",token:adminToken))check(r.IsSuccessStatusCode,"Explicit Admin unlock keeps pending first-login requirement");
        var created=new List<User>{newAdmin};foreach(var role in new[]{RoleCodes.Receptionist,RoleCodes.Dentist,RoleCodes.Assistant})created.Add(await Create(role));
        foreach(var u in created)
        {
            var prior=tokens.GenerateAccessToken(u);using(var r=await Send("/api/auth/login",new LoginRequest(u.Phone,u.Phone)))check(r.StatusCode==HttpStatusCode.Unauthorized&&(await r.Content.ReadAsStringAsync()).Contains("AUTH_ACTIVATION_REQUIRED"),"New staff cannot log in before OTP: "+u.Role.RoleCode);
            using(var r=await Send("/api/auth/me",token:prior,method:"GET"))check(r.StatusCode==HttpStatusCode.Unauthorized,"Even signed preactivation JWT is denied");
            sms.IsConfigured=false;using(var r=await Send(root+"request",new ActivationRequest(u.Phone,u.Phone)))check(r.StatusCode==HttpStatusCode.ServiceUnavailable,"Staff OTP fails closed without SMS config");sms.IsConfigured=true;
            var count=sms.Codes.Count;await Issue(u,"Incorrect123");check(sms.Codes.Count==count,"Wrong current staff password dispatches no SMS");
            if(u.UserId==newAdmin.UserId){var bad=await Issue(u);var right=sms.Codes[u.Phone].Code;var wrong=right=="000000"?"111111":"000000";for(var n=0;n<5;n++)check(await Verify(bad,wrong)==HttpStatusCode.BadRequest,"Staff wrong OTP rejected");check(await Verify(bad,right)==HttpStatusCode.BadRequest,"Staff fifth wrong attempt locks challenge");}
            var g=await Grant(u);using(var r=await Send("/api/auth/me",token:g.CompletionToken,method:"GET"))check(r.StatusCode==HttpStatusCode.Unauthorized,"Staff completion ticket is not JWT");
            await db.Entry(u).ReloadAsync();check(u.RequiresPhoneActivation&&u.PhoneVerifiedAtUtc is null,"Verify alone does not activate staff or fabricate timestamp");
            var keep=u.UserId==newAdmin.UserId;var results=await Task.WhenAll(Finish(g,keep),Finish(g,keep));check(results.Count(s=>s==HttpStatusCode.OK)==1&&results.Count(s=>s==HttpStatusCode.BadRequest)==1,"Staff completion has exactly one winner");
            await db.Entry(u).ReloadAsync();check(!u.RequiresPhoneActivation&&u.PhoneVerifiedAtUtc.HasValue&&u.TokenVersion==1&&hasher.Verify(keep?u.Phone:"Selected123",u.PasswordHash),"Staff activation persists actual verification and chosen password");
            using(var r=await Send("/api/auth/me",token:prior,method:"GET"))check(r.StatusCode==HttpStatusCode.Unauthorized,"Old preactivation JWT stays revoked after completion");
            using var login=await Send("/api/auth/login",new LoginRequest(u.Phone,keep?u.Phone:"Selected123"));check(login.IsSuccessStatusCode,"Activated staff can log in");var lr=(await login.Content.ReadFromJsonAsync<LoginResponse>())!;using var me=await Send("/api/auth/me",token:lr.AccessToken,method:"GET");check(me.IsSuccessStatusCode&&lr.RoleCode==u.Role.RoleCode,"Only account actual role is granted after activation");
        }
        failure.Create=true;var failedPhone="0967999998";using(var r=await Send("/api/staff",new{fullName="Synthetic audit failure",phone=failedPhone,roleCode=RoleCodes.Dentist},adminToken))check(r.StatusCode==HttpStatusCode.InternalServerError,"Required create audit failure returns error");failure.Create=false;check(!await db.Users.AsNoTracking().AnyAsync(u=>u.Phone==failedPhone),"Create audit rollback leaves no login-capable or pending account");
        var pending=await Create(RoleCodes.Assistant);var auditGrant=await Grant(pending);failure.Activate=true;check(await Finish(auditGrant)==HttpStatusCode.InternalServerError,"Activation audit failure aborts staff completion");failure.Activate=false;await db.Entry(pending).ReloadAsync();check(pending.RequiresPhoneActivation&&pending.PhoneVerifiedAtUtc is null&&pending.TokenVersion==0,"Activation rollback leaves pending credentials unchanged");check(await Finish(auditGrant)==HttpStatusCode.BadRequest,"Failed staff completion proof cannot replay");
        foreach(var what in new[]{"role","password","lock"}){var u=await Create(RoleCodes.Assistant);var g=await Grant(u);var row=await db.Users.SingleAsync(x=>x.UserId==u.UserId);if(what=="role")row.RoleId=roles[RoleCodes.Dentist].RoleId;else if(what=="password")row.PasswordHash=hash;else row.IsActive=false;await db.SaveChangesAsync();check(await Finish(g)==HttpStatusCode.BadRequest,"Changing "+what+" invalidates staff completion proof");}
        var expire=await Grant(pending);clock.Now+=TimeSpan.FromMinutes(5);check(await Finish(expire)==HttpStatusCode.BadRequest,"Staff completion proof expires at five minutes");
        await db.Users.Where(u=>u.UserId==admin.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.RoleId,roles[RoleCodes.Dentist].RoleId));using(var r=await Send("/api/staff",new{fullName="Synthetic stale actor",phone="0967999997",roleCode=RoleCodes.Dentist},adminToken))check(r.StatusCode==HttpStatusCode.Forbidden,"Stale Admin JWT cannot create after actual role changes");await db.Users.Where(u=>u.UserId==admin.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.RoleId,roles[RoleCodes.Admin].RoleId));
        await db.Users.Where(u=>u.UserId==admin.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.IsActive,false));using(var r=await Send("/api/staff",new{fullName="Synthetic locked actor",phone="0967999997",roleCode=RoleCodes.Dentist},adminToken))check(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,"Locked Admin cannot create staff with old JWT");await db.Users.Where(u=>u.UserId==admin.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.IsActive,true));
        var payload=new{fullName="Synthetic concurrent creation",phone="0967999996",roleCode=RoleCodes.Dentist};var race=await Task.WhenAll(Send("/api/staff",payload,adminToken),Send("/api/staff",payload,adminToken));check(race.Count(x=>x.StatusCode==HttpStatusCode.Created)==1&&race.Count(x=>x.StatusCode==HttpStatusCode.Conflict)==1,"Concurrent staff creation has one account and one conflict");foreach(var r in race)r.Dispose();
        check(await db.Users.AsNoTracking().CountAsync(u=>u.Phone==payload.phone)==1,"Unique staff phone preserved after concurrent creation");
        check(await db.AuditLogs.CountAsync(a=>a.Action=="MOD_AUTH_STAFF_ACTIVATED")==4,"Exactly four committed activation audits for four new staff roles");
        check(await db.AuditLogs.Where(a=>a.Action=="MOD_AUTH_STAFF_ACTIVATED"||a.Action==AuditActions.Create).AllAsync(a=>a.Detail!=null&&!a.Detail.Contains("0967")&&!a.Detail.Contains("Selected123")&&!a.Detail.Contains("PasswordHash")),"Creation and activation audits exclude credentials and phone");
        foreach(var u in legacy.Values){await db.Entry(u).ReloadAsync();check(!u.RequiresPhoneActivation&&u.PhoneVerifiedAtUtc is null&&u.TokenVersion==0&&u.PasswordHash==hash,"Legacy account evidence and credentials remain unchanged");using var r=await Send("/api/auth/me",token:oldTokens[u.UserId],method:"GET");check(r.IsSuccessStatusCode,"Legacy session remains valid at end of new staff flow");}
        await app.StopAsync();
    }
}
