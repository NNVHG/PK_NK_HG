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
using Dental.Application.Features.MOD_APP;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Features.Queue.Services;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

internal static class PatientActivationHttpSmoke
{
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now=DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow()=>Now; }
    private sealed class Sms : IPatientActivationDelivery
    {
        public bool IsConfigured {get;set;} = true;
        public bool Deliver=true;
        public Dictionary<string,ActivationDispatch> Codes=[];
        public bool Enqueue(ActivationDispatch dispatch) { Codes[dispatch.Phone]=dispatch; return Deliver; }
    }
    private sealed class AuditFailure : SaveChangesInterceptor
    {
        public bool Fail;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(Fail&&data.Context!.ChangeTracker.Entries<AuditLog>().Any(x=>x.State==EntityState.Added&&x.Entity.Action=="MOD_AUTH_PATIENT_ACTIVATED")) throw new InvalidOperationException("Synthetic activation audit failure");
            return base.SavingChangesAsync(data,result,ct);
        }
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options,Action<bool,string> check)
    {
        var clock=new Clock { Now = new DateTimeOffset(DateTime.UtcNow.Date.AddHours(3), TimeSpan.Zero) };var sms=new Sms();var failure=new AuditFailure();
        var dbOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure).Options;
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{Args=[],EnvironmentName="Testing"});
        builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
        var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));builder.Configuration["Jwt:Secret"]=secret;
        builder.Services.AddSingleton<TimeProvider>(clock);builder.Services.AddSingleton<VietnamClock>();
        builder.Services.AddSingleton<IPatientActivationDelivery>(sms);builder.Services.AddSingleton<IPatientActivationProofs,PatientActivationProofs>();
        builder.Services.AddScoped(_=>new DentalDbContext(dbOptions));builder.Services.AddScoped<IPatientActivationRepository,PatientActivationRepository>();
        builder.Services.AddScoped<PatientActivationService>();builder.Services.AddScoped<IPasswordHasher,BCryptPasswordHasher>();
        builder.Services.AddScoped<IUserRepository,UserRepository>();builder.Services.AddScoped<IPatientRepository,PatientRepository>();builder.Services.AddScoped<IUnitOfWork,UnitOfWork>();
        builder.Services.AddScoped<IAuditLogger,AuditLogger>();builder.Services.AddScoped<ITokenService,JwtTokenService>();builder.Services.AddScoped<AuthService>();builder.Services.AddScoped<LoginRequestValidator>();builder.Services.AddScoped<ChangePasswordRequestValidator>();
        builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<ICurrentUser,CurrentUser>();
        builder.Services.AddControllers().AddApplicationPart(typeof(PatientActivationController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt=>{
            jwt.TokenValidationParameters=new TokenValidationParameters{ValidateIssuer=true,ValidIssuer="DentalClinic",ValidateAudience=true,ValidAudience="DentalClinicClient",IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),ClockSkew=TimeSpan.Zero};
            jwt.Events=new JwtBearerEvents{OnTokenValidated=AccessTokenVersionValidation.ValidateAsync};
        });
        builder.Services.AddAuthorization();builder.Services.AddRateLimiter(o=>{AuthRateLimitPolicies.Configure(o,builder.Configuration);PatientActivationRateLimits.Configure(o);});
        await using var app=builder.Build();var ipSequence=0;
        // Synthetic IPs isolate individual scenarios; one explicit header value tests a shared real limiter partition.
        app.Use(async(c,next)=>{c.Connection.RemoteIpAddress=IPAddress.Parse(c.Request.Headers["X-Test-IP"].FirstOrDefault()??$"10.0.{Interlocked.Increment(ref ipSequence)/250}.{ipSequence%250+1}");try{await next(c);}catch{c.Response.StatusCode=500;}});
        app.UseAuthentication();app.UseRateLimiter();app.UseAuthorization();app.MapControllers();app.MapGet("/probe",()=>"Authorized").RequireAuthorization();await app.StartAsync();
        using var client=new HttpClient{BaseAddress=new Uri(app.Urls.Single())};await using var db=new DentalDbContext(dbOptions);
        var hasher=new BCryptPasswordHasher();var tokens=new JwtTokenService(builder.Configuration);var roles=new Dictionary<string,Role>();
        foreach(var code in new[]{RoleCodes.Admin,RoleCodes.Patient,RoleCodes.Dentist,RoleCodes.Receptionist,RoleCodes.Assistant}){var r=new Role{RoleCode=code,RoleName="Synthetic "+code};roles.Add(code,r);db.Roles.Add(r);}
        var oldHash=hasher.Hash("Legacy123");
        var admin=new User{Role=roles[RoleCodes.Admin],FullName="Synthetic staff",Phone="0967100000",PasswordHash=oldHash};
        var legacy=new User{Role=roles[RoleCodes.Patient],FullName="Synthetic legacy",Phone="0967100001",PasswordHash=oldHash};
        db.Users.AddRange(admin,legacy);await db.SaveChangesAsync();var sequence=2;var citizenIds=new Dictionary<int,string>();
        async Task<User> NewAccount()
        {
            var n=++sequence;var row=new BookingRequest{ClientRequestId=Guid.NewGuid(),IntentDigest=new string('A',64),FullName="Synthetic new "+n,Phone="09671"+n.ToString("D5"),DateOfBirth=new(1990,1,1),Gender="Other",AppointmentDate=new VietnamClock(clock).Today.AddDays(30+n),SlotTime=new(8,30),SlotDurationMinutes=30};
            db.BookingRequests.Add(row);await db.SaveChangesAsync();
            await using var approvalDb=new DentalDbContext(dbOptions);var repo=new BookingApprovalRepository(approvalDb,new VietnamClock(clock),hasher);
            var c=(await repo.ReadAsync(row.Id,admin.UserId,RoleCodes.Admin,default)).Value!;
            var r=await repo.ApproveAsync(row.Id,admin.UserId,RoleCodes.Admin,new(c.Version,c.ReviewStamp,null,true,false,true),null,default);
            check(r.IsSuccess&&!r.Value!.CreatedAccount&&!r.Value.RequiresPhoneActivation,"Booking approval defers account creation until verified check-in");
            var citizenId="012345"+n.ToString("D6");
            await using var checkInDb=new DentalDbContext(dbOptions);
            var patients=new PatientRepository(checkInDb);var visits=new VisitRepository(checkInDb);
            var audit=new AuditLogger(checkInDb,NullLogger<AuditLogger>.Instance);var vietnamClock=new VietnamClock(clock);
            var queue=new QueueService(new QueueRepository(checkInDb),new AppointmentRepository(checkInDb),patients,visits,
                new InvoiceRepository(checkInDb),new UserRepository(checkInDb),new UnitOfWork(checkInDb),audit,vietnamClock,
                new InvoiceDraftService(new InvoiceRepository(checkInDb),visits,patients,audit,vietnamClock),TestChairFixtures.Service(checkInDb,vietnamClock),hasher);
            var checkedIn=await queue.CheckInAsync(admin.UserId,new CheckInRequest(r.Value!.PatientId,CitizenId:citizenId,Phone:row.Phone),null);
            check(checkedIn.IsSuccess,"Verified check-in creates a personal Patient account");
            var user=await db.Users.AsNoTracking().Include(u=>u.Role).SingleAsync(u=>u.Phone==row.Phone);
            check(user.RequiresPhoneActivation&&user.LoginByCitizenId,"New Patient account requires OTP and CCCD login");
            citizenIds[user.UserId]=citizenId;return user;
        }
        string Identifier(User u)=>citizenIds.TryGetValue(u.UserId,out var citizenId)?citizenId:u.Phone;
        const string root="/api/auth/activation/";
        async Task<HttpResponseMessage> Send(string path,object body,string? ip=null){using var req=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};if(ip is not null)req.Headers.Add("X-Test-IP",ip);return await client.SendAsync(req);}
        async Task<ActivationChallenge> Issue(User u,string? password=null)
        {
            using var r=await Send(root+"request",new ActivationRequest(Identifier(u),password??u.Phone));
            check(r.StatusCode==HttpStatusCode.Accepted&&r.Headers.CacheControl?.NoStore==true,"Activation request is generic 202 and NoStore");
            var json=await r.Content.ReadAsStringAsync();check(!json.Contains(u.Phone)&&!json.Contains(Identifier(u))&&!json.Contains("passwordHash",StringComparison.OrdinalIgnoreCase)&&!json.Contains("\"code\"",StringComparison.OrdinalIgnoreCase)&&!json.Contains("userId",StringComparison.OrdinalIgnoreCase),"Activation response excludes identity and OTP");
            return(await r.Content.ReadFromJsonAsync<ActivationChallenge>())!;
        }
        async Task<HttpStatusCode> Verify(ActivationChallenge c,string code){using var r=await Send(root+"verify",new ActivationVerification(c.ChallengeId,code));return r.StatusCode;}
        async Task<ActivationGrant> Grant(User u)
        {
            clock.Now+=TimeSpan.FromSeconds(61);var c=await Issue(u);using var r=await Send(root+"verify",new ActivationVerification(c.ChallengeId,sms.Codes[u.Phone].Code));
            check(r.StatusCode==HttpStatusCode.OK&&r.Headers.CacheControl?.NoStore==true,"Correct OTP yields only short purpose-bound completion proof");
            return(await r.Content.ReadFromJsonAsync<ActivationGrant>())!;
        }
        async Task<HttpStatusCode> Finish(ActivationGrant g,bool keep=false,string? password="Strong123"){using var r=await Send(root+"complete",new ActivationCompletion(g.CompletionToken,keep,password,true));check(r.Headers.CacheControl?.NoStore==true||r.StatusCode==HttpStatusCode.InternalServerError,"Completion never caches secrets");return r.StatusCode;}
        async Task<HttpResponseMessage> Login(User u,string password)=>await Send("/api/auth/login",new LoginRequest(Identifier(u),password));
        async Task<HttpStatusCode> Probe(string token){using var req=new HttpRequestMessage(HttpMethod.Get,"/probe");req.Headers.Authorization=new("Bearer",token);using var r=await client.SendAsync(req);return r.StatusCode;}
        var legacyToken=tokens.GenerateAccessToken(legacy);check(await Probe(legacyToken)==HttpStatusCode.OK,"Legacy JWT continues to authorize without fabricated OTP");
        using(var r=await Login(legacy,"Legacy123"))check(r.StatusCode==HttpStatusCode.OK,"Legacy account still logs in without OTP");
        check(legacy.PhoneVerifiedAtUtc is null&&!legacy.RequiresPhoneActivation,"Existing account has no artificial verification timestamp");
        var account=await NewAccount();var staleToken=tokens.GenerateAccessToken(account);
        using(var r=await Login(account,account.Phone))check(r.StatusCode==HttpStatusCode.Unauthorized&&(await r.Content.ReadAsStringAsync()).Contains("AUTH_ACTIVATION_REQUIRED"),"New approved account cannot receive JWT before activation");
        check(await Probe(staleToken)==HttpStatusCode.Unauthorized,"Even a correctly signed preactivation JWT is rejected");
        sms.IsConfigured=false;using(var r=await Send(root+"request",new ActivationRequest(Identifier(account),account.Phone)))check(r.StatusCode==HttpStatusCode.ServiceUnavailable&&sms.Codes.Count==0,"Missing SMS configuration fails closed");sms.IsConfigured=true;
        foreach(var u in new[]{legacy,admin}){var before=sms.Codes.Count;await Issue(u,"Legacy123");check(sms.Codes.Count==before,"Old Patient and staff accounts cannot be activated");}
        var unknown=new User{Phone="0967199999"};await Issue(unknown);check(!sms.Codes.ContainsKey(unknown.Phone),"Unknown account returns metadata without SMS");
        await Issue(account,"Wrong123");check(!sms.Codes.ContainsKey(account.Phone),"Incorrect current password never sends activation SMS");
        clock.Now+=TimeSpan.FromSeconds(61);var locked=await Issue(account);var good=sms.Codes[account.Phone].Code;var wrong=good=="000000"?"000001":"000000";
        using(var r=await Send(root+"request",new ActivationRequest(Identifier(account),account.Phone)))check(r.StatusCode==HttpStatusCode.TooManyRequests,"Resend within 60 seconds is rejected");
        for(var n=0;n<5;n++)check(await Verify(locked,wrong)==HttpStatusCode.BadRequest,"Incorrect OTP attempt rejected");check(await Verify(locked,good)==HttpStatusCode.BadRequest,"Fifth wrong attempt blocks even the correct OTP");
        clock.Now+=TimeSpan.FromSeconds(61);var expired=await Issue(account);good=sms.Codes[account.Phone].Code;clock.Now+=TimeSpan.FromMinutes(5);check(await Verify(expired,good)==HttpStatusCode.BadRequest,"OTP expires exactly at five minutes");
        var first=await Issue(account);var firstCode=sms.Codes[account.Phone].Code;clock.Now+=TimeSpan.FromSeconds(61);var second=await Issue(account);check(await Verify(first,firstCode)==HttpStatusCode.BadRequest,"Resend invalidates previous OTP challenge");
        var grant=await Grant(account);check(await Probe(grant.CompletionToken)==HttpStatusCode.Unauthorized,"Completion proof cannot authorize protected API as JWT");
        await db.Entry(account).ReloadAsync();check(account.RequiresPhoneActivation&&account.PhoneVerifiedAtUtc is null&&hasher.Verify(account.Phone,account.PasswordHash),"OTP verification alone does not activate account or change password");
        var hash=account.PasswordHash;
        var results=await Task.WhenAll(Finish(grant),Finish(grant));check(results.Count(x=>x==HttpStatusCode.OK)==1&&results.Count(x=>x==HttpStatusCode.BadRequest)==1,$"Concurrent completion commits exactly one activation ({string.Join(",",results)})");
        await db.Entry(account).ReloadAsync();check(!account.RequiresPhoneActivation&&account.PhoneVerifiedAtUtc.HasValue&&Math.Abs((account.PhoneVerifiedAtUtc.Value-clock.Now.UtcDateTime).TotalMilliseconds)<1&&account.TokenVersion==1&&account.PasswordHash!=hash&&hasher.Verify("Strong123",account.PasswordHash),"Patient activation records OTP time, revokes old token and replaces initial password");
        check(await Probe(staleToken)==HttpStatusCode.Unauthorized,"Preactivation JWT remains invalid after completion");
        using(var r=await Login(account,account.Phone))check(r.StatusCode==HttpStatusCode.Unauthorized,"Initial phone password cannot log in after activation");
        using(var r=await Login(account,"Strong123")){check(r.StatusCode==HttpStatusCode.OK,"Activated account logs in with chosen password");var login=(await r.Content.ReadFromJsonAsync<LoginResponse>())!;check(await Probe(login.AccessToken)==HttpStatusCode.OK,"Postactivation login JWT authorizes protected API");}
        var changed=await NewAccount();var chosen=await Grant(changed);
        using(var r=await Send(root+"complete",new ActivationCompletion(chosen.CompletionToken,false,"Weak123",true)))check(r.StatusCode==HttpStatusCode.BadRequest,"Chosen password below eight characters is rejected without consuming proof");
        check(await Finish(chosen,false,"Strong123")==HttpStatusCode.OK,"Chosen valid password completes activation");await db.Entry(changed).ReloadAsync();check(hasher.Verify("Strong123",changed.PasswordHash)&&!hasher.Verify(changed.Phone,changed.PasswordHash),"Selected password persists as bcrypt and replaces default");
        using(var r=await Login(changed,"Strong123"))check(r.IsSuccessStatusCode,"New selected password can log in");using(var r=await Login(changed,changed.Phone))check(r.StatusCode==HttpStatusCode.Unauthorized,"Replaced default password no longer logs in");
        var auditAccount=await NewAccount();var auditGrant=await Grant(auditAccount);failure.Fail=true;check(await Finish(auditGrant)==HttpStatusCode.InternalServerError,"Required audit failure aborts activation");failure.Fail=false;
        await db.Entry(auditAccount).ReloadAsync();check(auditAccount.RequiresPhoneActivation&&auditAccount.PhoneVerifiedAtUtc is null&&auditAccount.TokenVersion==0,"Audit rollback preserves pending state and credential version");check(await Finish(auditGrant)==HttpStatusCode.BadRequest,"Failed write proof cannot be replayed");
        var expiryGrant=await Grant(auditAccount);clock.Now+=TimeSpan.FromMinutes(5);check(await Finish(expiryGrant)==HttpStatusCode.BadRequest,"Completion proof expires exactly at five minutes");
        foreach(var change in new[]{"password","phone","role","inactive","patient"})
        {
            var u=await NewAccount();var g=await Grant(u);
            if(change=="patient")await db.Patients.Where(p=>p.UserId==u.UserId).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.UserId,(int?)legacy.UserId));
            else {var row=await db.Users.SingleAsync(x=>x.UserId==u.UserId);switch(change){case "password":row.PasswordHash=oldHash;break;case "phone":row.Phone="09672"+(++sequence).ToString("D5");break;case "role":row.RoleId=roles[RoleCodes.Dentist].RoleId;break;case "inactive":row.IsActive=false;break;}await db.SaveChangesAsync();}
            check(await Finish(g)==HttpStatusCode.BadRequest,"Changed "+change+" invalidates pending completion");
            check(await db.Users.AsNoTracking().AnyAsync(x=>x.UserId==u.UserId&&x.RequiresPhoneActivation&&x.PhoneVerifiedAtUtc==null),"Invalidated account remains unverified");
        }
        clock.Now+=TimeSpan.FromSeconds(61);var changedDuringOtp=await Issue(auditAccount);await db.Users.Where(u=>u.UserId==auditAccount.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.IsActive,false));check(await Verify(changedDuringOtp,sms.Codes[auditAccount.Phone].Code)==HttpStatusCode.BadRequest,"Account locked after dispatch cannot obtain completion proof");
        var count=sms.Codes.Count;clock.Now+=TimeSpan.FromSeconds(61);await Issue(auditAccount);check(sms.Codes.Count==count,"Locked account request remains generic without SMS");
        var deliveryAccount=await NewAccount();sms.Deliver=false;var undelivered=await Issue(deliveryAccount);check(await Verify(undelivered,sms.Codes[deliveryAccount.Phone].Code)==HttpStatusCode.BadRequest,"Delivery failure revokes proof rather than accepting unseen OTP");sms.Deliver=true;
        var usersBefore=await db.Users.CountAsync();var patientsBefore=await db.Patients.CountAsync();
        foreach(var payload in new object[]{new{phone="0967199998",password="Strong123",fullName="Synthetic blocked register"},new{invalid=true}}){using var r=await Send("/api/auth/register",payload);check(r.StatusCode==HttpStatusCode.Gone&&r.Headers.CacheControl?.NoStore==true,"Legacy registration is disabled with explicit 410 and NoStore");}
        check(await db.Users.CountAsync()==usersBefore&&await db.Patients.CountAsync()==patientsBefore,"Legacy registration writes neither User nor Patient");
        check(await db.AuditLogs.CountAsync(x=>x.Action=="MOD_AUTH_PATIENT_ACTIVATED")==2,"Exactly one activation audit for each committed activation");
        check(await db.AuditLogs.Where(x=>x.Action=="MOD_AUTH_PATIENT_ACTIVATED").AllAsync(x=>x.Detail!=null&&!x.Detail.Contains("09671")&&!x.Detail.Contains("Strong123")&&!x.Detail.Contains("completionToken")),"Activation audit contains no phone, password or proof");
        await db.Entry(legacy).ReloadAsync();check(legacy.PhoneVerifiedAtUtc is null&&legacy.TokenVersion==0&&!legacy.RequiresPhoneActivation&&await Probe(legacyToken)==HttpStatusCode.OK,"Legacy account and session remain unchanged throughout new activation");
        var oldCredential=new User{Role=roles[RoleCodes.Patient],FullName="Synthetic password boundary",Phone="0967300000",PasswordHash=hasher.Hash("Aa1234")};db.Users.Add(oldCredential);await db.SaveChangesAsync();
        using(var r=await Login(oldCredential,"Aa1234"))check(r.StatusCode==HttpStatusCode.OK,"Existing six-character password still logs in under DL052");
        var credentialToken=tokens.GenerateAccessToken(oldCredential);
        using(var r=await Send("/api/auth/change-password",new ChangePasswordRequest("Aa1234","Aa123456")))check(r.StatusCode==HttpStatusCode.Unauthorized,"Password change still requires JWT");
        async Task<HttpStatusCode> Change(string password){using var req=new HttpRequestMessage(HttpMethod.Post,"/api/auth/change-password"){Content=JsonContent.Create(new ChangePasswordRequest("Aa1234",password))};req.Headers.Authorization=new("Bearer",credentialToken);using var r=await client.SendAsync(req);return r.StatusCode;}
        foreach(var password in new[]{"Aa1234","Aa12345","Strong1\0x","Aa1"+new string('Đ',35)})check(await Change(password)==HttpStatusCode.BadRequest,"HTTP password change rejects short or bcrypt-invalid selected credential");
        await db.Entry(oldCredential).ReloadAsync();check(hasher.Verify("Aa1234",oldCredential.PasswordHash),"Rejected changes preserve legacy credential");
        check(await Change("Aa123456")==HttpStatusCode.OK,"HTTP password change accepts eight-character policy boundary");
        using(var r=await Login(oldCredential,"Aa123456"))check(r.StatusCode==HttpStatusCode.OK,"Password chosen at boundary logs in");
        using(var r=await Login(oldCredential,"Aa1234"))check(r.StatusCode==HttpStatusCode.Unauthorized,"Replaced legacy password no longer logs in");
        var rateStatuses=new List<HttpStatusCode>();for(var n=0;n<11;n++){using var r=await Send(root+"request",new ActivationRequest("09679"+n.ToString("D5"),"Invalid123"),"10.99.99.99");rateStatuses.Add(r.StatusCode);}check(rateStatuses.Take(10).All(s=>s==HttpStatusCode.Accepted)&&rateStatuses[10]==HttpStatusCode.TooManyRequests,"Actual IP limiter rejects eleventh activation request across different numbers");
        await app.StopAsync();
    }
}
