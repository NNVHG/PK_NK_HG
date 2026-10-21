using System.Text.Json;
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
internal static class PatientPhoneChangeHttpSmoke
{
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Sms : IPatientPhoneChangeSms
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
            if (Enabled && d.Context!.ChangeTracker.Entries<AuditLog>().Any(e=>e.State==EntityState.Added&&(e.Entity.Action=="MOD_AUTH_PATIENT_PHONE_CHANGED"||e.Entity.Action=="MOD_PAT_CONTACT_PHONE_SYNCED"||e.Entity.Action=="MOD_AUTH_COUNTER_PHONE_REQUESTED"||e.Entity.Action=="MOD_AUTH_COUNTER_PHONE_CONFIRMED")))
                throw new InvalidOperationException("Synthetic required audit failure");
            return base.SavingChangesAsync(d,result,ct);
        }
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options,Action<bool,string> check)
    {
        var clock=new Clock();var sms=new Sms();var failure=new Failure();var dbOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure).Options;
        var b=WebApplication.CreateBuilder(new WebApplicationOptions{Args=[],EnvironmentName="Testing"});b.Logging.ClearProviders();b.WebHost.UseUrls("http://127.0.0.1:0");
        var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));b.Configuration["Jwt:Secret"]=secret;
        b.Services.AddSingleton<TimeProvider>(clock);b.Services.AddSingleton<VietnamClock>();b.Services.AddSingleton<IPatientPhoneChangeSms>(sms);b.Services.AddSingleton<IPatientPhoneChangeProofs,PatientPhoneChangeProofs>();
        b.Services.AddScoped(_=>new DentalDbContext(dbOptions));b.Services.AddScoped<IPatientPhoneChangeRepository,PatientPhoneChangeRepository>();b.Services.AddScoped<PatientPhoneChangeService>();b.Services.AddScoped<ICounterPatientPhoneRepository,CounterPatientPhoneRepository>();b.Services.AddScoped<CounterPatientPhoneService>();
        b.Services.AddScoped<IPasswordHasher,BCryptPasswordHasher>();b.Services.AddScoped<IUserRepository,UserRepository>();b.Services.AddScoped<IPatientRepository,PatientRepository>();b.Services.AddScoped<IUnitOfWork,UnitOfWork>();b.Services.AddScoped<IAuditLogger,AuditLogger>();b.Services.AddScoped<ITokenService,JwtTokenService>();
        b.Services.AddScoped<AuthService>();b.Services.AddScoped<LoginRequestValidator>();
        b.Services.AddHttpContextAccessor();b.Services.AddScoped<ICurrentUser,CurrentUser>();b.Services.AddControllers().AddApplicationPart(typeof(PatientPhoneChangeController).Assembly);
        b.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt=>{jwt.TokenValidationParameters=new TokenValidationParameters{ValidateIssuer=true,ValidIssuer="DentalClinic",ValidateAudience=true,ValidAudience="DentalClinicClient",IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),ClockSkew=TimeSpan.Zero};jwt.Events=new JwtBearerEvents{OnTokenValidated=AccessTokenVersionValidation.ValidateAsync};});
        b.Services.AddAuthorization(o=>o.AddApplicationPolicies());b.Services.AddRateLimiter(o=>{AuthRateLimitPolicies.Configure(o,b.Configuration);PatientPhoneChangeRateLimits.Configure(o);});
        await using var app=b.Build();var ip=0;var fixedIp=false;app.Use(async(c,next)=>{var n=fixedIp?60000:Interlocked.Increment(ref ip);c.Connection.RemoteIpAddress=IPAddress.Parse($"10.2.{n/250}.{n%250+1}");try{await next(c);}catch{c.Response.StatusCode=500;}});app.UseAuthentication();app.UseRateLimiter();app.UseAuthorization();app.MapControllers();await app.StartAsync();
        using var client=new HttpClient{BaseAddress=new Uri(app.Urls.Single())};await using var db=new DentalDbContext(dbOptions);var hasher=new BCryptPasswordHasher();var tokens=new JwtTokenService(b.Configuration);var hash=hasher.Hash("Legacy123");var seq=0;
        var roles=new Dictionary<string,Role>();foreach(var code in new[]{RoleCodes.Admin,RoleCodes.Receptionist,RoleCodes.Dentist,RoleCodes.Assistant,RoleCodes.Patient}){var role=new Role{RoleCode=code,RoleName="Synthetic "+code};roles.Add(code,role);db.Roles.Add(role);}await db.SaveChangesAsync();
        async Task<User> Create(string role=RoleCodes.Patient,bool verified=false,bool pending=false)
        {var u=new User{Role=roles[role],Phone="09687"+(++seq).ToString("D5"),FullName="Synthetic account "+seq,PasswordHash=hash,PhoneVerifiedAtUtc=verified?clock.Now.UtcDateTime:null,RequiresPhoneActivation=pending};db.Users.Add(u);await db.SaveChangesAsync();return u;}
        string NewPhone()=>"09688"+(++seq).ToString("D5");
        async Task<HttpResponseMessage> Send(string path,object? data=null,string? token=null,string method="POST")
        {using var request=new HttpRequestMessage(new HttpMethod(method),path);if(data is not null)request.Content=JsonContent.Create(data);if(token is not null)request.Headers.Authorization=new("Bearer",token);return await client.SendAsync(request);}
        const string root="/api/auth/patient-phone-change/";
        async Task<Patient> AddProfile(User? owner,string phone,bool active=true)
        {var p=new Patient{User=owner,Phone=phone,FullName="Synthetic family "+(++seq),DateOfBirth=new DateOnly(2000,1,1),IsActive=active,Address="Synthetic unchanged",Email="synthetic@example.invalid"};db.Patients.Add(p);await db.SaveChangesAsync();return p;}
        async Task<PatientPhonePreview> Preview(User u,string phone)
        {using var r=await Send(root+"preview",new PatientPhonePreviewRequest(phone),tokens.GenerateAccessToken(u));check(r.StatusCode==HttpStatusCode.OK&&r.Headers.CacheControl?.NoStore==true,"Owner preview is authorized and NoStore");return(await r.Content.ReadFromJsonAsync<PatientPhonePreview>())!;}
        async Task<PatientPhonePending> Issue(User u,PatientPhonePreview p)
        {clock.Now+=TimeSpan.FromSeconds(61);using var r=await Send(root+"request",new PatientPhoneRequest(p.NewPhone,p.ReviewStamp,true),tokens.GenerateAccessToken(u));check(r.StatusCode==HttpStatusCode.Accepted&&r.Headers.CacheControl?.NoStore==true,"Reviewed family scope issues OTP with NoStore");var text=await r.Content.ReadAsStringAsync();check(!text.Contains("code",StringComparison.OrdinalIgnoreCase)&&!text.Contains("stamp",StringComparison.OrdinalIgnoreCase),"OTP response exposes no code or server binding");return(await r.Content.ReadFromJsonAsync<PatientPhonePending>())!;}
        async Task<HttpResponseMessage> Confirm(User u,PatientPhonePreview p,PatientPhonePending pending,string? code=null,string? token=null)
        {var r=await Send(root+"confirm",new PatientPhoneConfirmation(pending.ChallengeId,code??sms.Codes[p.NewPhone],p.ReviewStamp,true),token??tokens.GenerateAccessToken(u));if(r.StatusCode!=HttpStatusCode.Unauthorized)check(r.Headers.CacheControl?.NoStore==true,"Confirmation response is NoStore");return r;}
        async Task<bool> Session(string token){using var r=await Send("/api/auth/me",token:token,method:"GET");return r.IsSuccessStatusCode;}
        // Q350b: a persisted counter suggestion grants no authority to change the account.
        var clerk=await Create(RoleCodes.Receptionist);var admin=await Create(RoleCodes.Admin);var family=await Create();var familyOld=family.Phone;
        var familyMember=await AddProfile(family,family.Phone);var familyOther=await AddProfile(family,"0968000000");
        async Task<Appointment> Book(Patient patient,string status=AppointmentStatuses.Scheduled)
        {var a=new Appointment{PatientId=patient.PatientId,AppointmentDate=new DateOnly(2030,1,1),SlotTime=new TimeOnly(8,0),CreatedByUserId=clerk.UserId,Status=status};db.Appointments.Add(a);await db.SaveChangesAsync();return a;}
        var booking=await Book(familyMember);var counterPhone=NewPhone();var counterPath=$"/api/appointments/{booking.AppointmentId}/patient-phone-change";
        async Task<HttpResponseMessage> Counter(User? actor,string? phone=null,bool consent=true,int? appointmentId=null)
            => await Send(appointmentId.HasValue?$"/api/appointments/{appointmentId}/patient-phone-change":counterPath,new CounterPhoneRequest(phone??counterPhone,consent),actor is null?null:tokens.GenerateAccessToken(actor));
        async Task<CounterPhoneSuggestion?> Own(User u)
        {using var r=await Send(root+"counter-request",token:tokens.GenerateAccessToken(u),method:"GET");check(r.IsSuccessStatusCode&&r.Headers.CacheControl?.NoStore==true,"Owner-only counter suggestion is NoStore");var json=System.Text.Json.JsonDocument.Parse(await r.Content.ReadAsStringAsync());return json.RootElement.GetProperty("request").ValueKind==System.Text.Json.JsonValueKind.Null?null:json.RootElement.GetProperty("request").Deserialize<CounterPhoneSuggestion>(new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive=true});}
        using(var r=await Counter(null))check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous cannot initiate counter change");
        foreach(var code in new[]{RoleCodes.Patient,RoleCodes.Dentist,RoleCodes.Assistant}){var denied=await Create(code);using var r=await Counter(denied);check(r.StatusCode==HttpStatusCode.Forbidden,"Counter initiation denies "+code);}
        using(var r=await Counter(clerk,consent:false))check(r.StatusCode==HttpStatusCode.BadRequest,"Counter reconciliation consent required");
        using(var r=await Counter(clerk,"0967000001\n"))check(r.StatusCode==HttpStatusCode.BadRequest,"Counter number rejects trailing newline");
        using(var r=await Counter(clerk,family.Phone))check(r.StatusCode==HttpStatusCode.Conflict,"Counter cannot request unchanged number");
        using(var r=await Counter(clerk,admin.Phone))check(r.StatusCode==HttpStatusCode.Conflict,"Counter rejects occupied login");
        using(var r=await Counter(clerk,appointmentId:int.MaxValue))check(r.StatusCode==HttpStatusCode.NotFound,"Counter binds existing appointment");
        foreach(var status in new[]{AppointmentStatuses.Cancelled,AppointmentStatuses.Completed,AppointmentStatuses.CheckedIn}){var statusBooking=await Book(familyMember,status);using var r=await Counter(clerk,appointmentId:statusBooking.AppointmentId);check(r.StatusCode==HttpStatusCode.Conflict,"Counter rejects appointment "+status);}
        var pastBooking=await Book(familyMember);await db.Appointments.Where(x=>x.AppointmentId==pastBooking.AppointmentId).ExecuteUpdateAsync(x=>x.SetProperty(a=>a.AppointmentDate,new DateOnly(2000,1,1)));using(var r=await Counter(clerk,appointmentId:pastBooking.AppointmentId))check(r.StatusCode==HttpStatusCode.Conflict,"Past scheduled appointment cannot initiate a new counter change");
        var unlinked=await AddProfile(null,family.Phone);var unlinkedBooking=await Book(unlinked);using(var r=await Counter(clerk,appointmentId:unlinkedBooking.AppointmentId))check(r.StatusCode==HttpStatusCode.Conflict,"Unlinked patient does not resolve account by phone");
        var pendingOwner=await Create(pending:true);var pendingMember=await AddProfile(pendingOwner,pendingOwner.Phone);var pendingBooking=await Book(pendingMember);using(var r=await Counter(clerk,appointmentId:pendingBooking.AppointmentId))check(r.StatusCode==HttpStatusCode.Conflict,"Pending activation is not changed through JWT counter path");
        check(await Own(family) is null,"No suggestion has explicit null envelope");var beforeSms=sms.Codes.Count;
        Guid counterId;
        using(var r=await Counter(clerk)){check(r.StatusCode==HttpStatusCode.Accepted&&r.Headers.CacheControl?.NoStore==true,"Receptionist initiates with NoStore");var receipt=(await r.Content.ReadFromJsonAsync<CounterPhoneReceipt>())!;check(receipt.RequiresOwnerConfirmation&&receipt.RequestId!=Guid.Empty,"Counter returns receipt only, owner must confirm");counterId=receipt.RequestId;var text=await r.Content.ReadAsStringAsync();check(!text.Contains(counterPhone)&&!text.Contains("code",StringComparison.OrdinalIgnoreCase)&&!text.Contains("profiles",StringComparison.OrdinalIgnoreCase),"Counter sees no OTP, family scope or phone echo");}
        check(sms.Codes.Count==beforeSms,"Counter initiation does not send or mint OTP");await db.Entry(family).ReloadAsync();await db.Entry(familyMember).ReloadAsync();check(family.Phone==familyOld&&family.TokenVersion==0&&family.PhoneVerifiedAtUtc is null&&family.PasswordHash==hash&&familyMember.Phone==familyOld,"Initiation preserves login, sessions, verification, password and contacts");check(await Session(tokens.GenerateAccessToken(family)),"Owner session remains usable pending counter suggestion");
        var suggested=await Own(family);check(suggested?.RequestId==counterId&&suggested.NewPhone==counterPhone&&suggested.AppointmentId==booking.AppointmentId,"Only owner receives own proposed number and booking");check(await Own(await Create()) is null,"Other patient cannot retrieve suggestion");
        foreach(var actor in new[]{clerk,admin}){using var r=await Send(root+"counter-request",token:tokens.GenerateAccessToken(actor),method:"GET");check(r.StatusCode==HttpStatusCode.Forbidden,"Staff cannot read owner confirmation scope");}
        using(var r=await Counter(admin))check(r.StatusCode==HttpStatusCode.Conflict,"Admin cannot overwrite current request");check(await db.PatientPhoneChangeRequests.CountAsync(x=>x.UserId==family.UserId)==1,"Duplicate initiation writes no duplicate row");
        var staffAudit=await db.AuditLogs.SingleAsync(x=>x.Action=="MOD_AUTH_COUNTER_PHONE_REQUESTED"&&x.UserId==clerk.UserId);check(staffAudit.Detail!.Contains(counterId.ToString())&&!staffAudit.Detail.Contains(counterPhone)&&staffAudit.Detail.Contains("DL-335"),"Initiation audit identifies actor and request, never phone");
        var familyScope=await Preview(family,counterPhone);var familyProof=await Issue(family,familyScope);
        using(var r=await Send(root+"confirm",new PatientPhoneConfirmation(familyProof.ChallengeId,sms.Codes[counterPhone],familyScope.ReviewStamp,true),tokens.GenerateAccessToken(clerk)))check(r.StatusCode==HttpStatusCode.Forbidden,"Even knowing OTP does not let counter staff confirm");
        failure.Enabled=true;using(var r=await Confirm(family,familyScope,familyProof))check(r.StatusCode==HttpStatusCode.InternalServerError,"Owner completion audit failure rolls back counter and contacts");failure.Enabled=false;
        check((await db.PatientPhoneChangeRequests.AsNoTracking().SingleAsync(x=>x.Id==counterId)).CompletedAtUtc is null,"Failed completion keeps counter request pending");await db.Entry(family).ReloadAsync();check(family.Phone==familyOld&&family.TokenVersion==0,"Failed counter completion preserves owner version");
        familyScope=await Preview(family,counterPhone);familyProof=await Issue(family,familyScope);using(var r=await Confirm(family,familyScope,familyProof))check(r.IsSuccessStatusCode,"Only owner final consent and OTP complete requested phone");
        check((await db.PatientPhoneChangeRequests.AsNoTracking().SingleAsync(x=>x.Id==counterId)).CompletedAtUtc.HasValue,"Counter request completes in same phone transaction");await db.Entry(family).ReloadAsync();check(await Own(family) is null,"Completed suggestion no longer returned");
        var completedAudit=await db.AuditLogs.SingleAsync(x=>x.Action=="MOD_AUTH_COUNTER_PHONE_CONFIRMED"&&x.UserId==family.UserId);check(completedAudit.Detail!.Contains(counterId.ToString())&&completedAudit.Detail.Contains("initiatedByUserId")&&!completedAudit.Detail.Contains(counterPhone),"Owner completion audit links counter initiator without sensitive values");
        await db.Entry(familyMember).ReloadAsync();await db.Entry(familyOther).ReloadAsync();check(familyMember.Phone==counterPhone&&familyOther.Phone=="0968000000","Counter path preserves exact DL334 contact scope");
        var second=await Create();var secondMember=await AddProfile(second,second.Phone);var secondBooking=await Book(secondMember);failure.Enabled=true;using(var r=await Counter(admin,NewPhone(),appointmentId:secondBooking.AppointmentId))check(r.StatusCode==HttpStatusCode.InternalServerError,"Required initiation audit failure returns failure");failure.Enabled=false;check(!await db.PatientPhoneChangeRequests.AnyAsync(x=>x.UserId==second.UserId),"Failed initiation audit commits no request");
        using(var r=await Counter(admin,NewPhone(),appointmentId:secondBooking.AppointmentId))check(r.StatusCode==HttpStatusCode.Accepted,"Admin can initiate after failed transaction");
        await db.Users.Where(x=>x.UserId==second.UserId).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.TokenVersion,1));await db.Entry(second).ReloadAsync();check(await Own(second) is null,"Credential revision makes old suggestion stale");using(var r=await Counter(admin,NewPhone(),appointmentId:secondBooking.AppointmentId))check(r.StatusCode==HttpStatusCode.Accepted,"Stale request does not block next credential revision");
        var downgraded=await Create(RoleCodes.Receptionist);var forged=tokens.GenerateAccessToken(downgraded);await db.Users.Where(x=>x.UserId==downgraded.UserId).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.RoleId,roles[RoleCodes.Patient].RoleId));using(var r=await Send(counterPath,new CounterPhoneRequest(NewPhone(),true),forged))check(r.StatusCode==HttpStatusCode.Forbidden,"Initiation checks active role in DB, not stale JWT role");
        var raceFamily=await Create();var raceMember=await AddProfile(raceFamily,raceFamily.Phone);var raceBooking=await Book(raceMember);
        var initiations=await Task.WhenAll(Counter(clerk,NewPhone(),appointmentId:raceBooking.AppointmentId),Counter(admin,NewPhone(),appointmentId:raceBooking.AppointmentId));
        check(initiations.Count(x=>x.StatusCode==HttpStatusCode.Accepted)==1&&initiations.Count(x=>x.StatusCode==HttpStatusCode.Conflict)==1,"Concurrent counter requests produce one winner and one conflict");foreach(var response in initiations)response.Dispose();
        check(await db.PatientPhoneChangeRequests.CountAsync(x=>x.UserId==raceFamily.UserId)==1,"Concurrent initiation leaves one durable suggestion");
        var lockedOwner=await Create();var lockedMember=await AddProfile(lockedOwner,lockedOwner.Phone);var lockedBooking=await Book(lockedMember);await db.Users.Where(x=>x.UserId==lockedOwner.UserId).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.IsActive,false));using(var r=await Counter(clerk,NewPhone(),appointmentId:lockedBooking.AppointmentId))check(r.StatusCode==HttpStatusCode.Conflict,"Counter cannot change locked owner account");
        var inactiveMember=await AddProfile(family,family.Phone,false);var inactiveBooking=await Book(inactiveMember);using(var r=await Counter(clerk,NewPhone(),appointmentId:inactiveBooking.AppointmentId))check(r.StatusCode==HttpStatusCode.Conflict,"Inactive appointment profile cannot initiate");
        var owner=await Create();var ownerToken=tokens.GenerateAccessToken(owner);var oldPhone=owner.Phone;
        var active=await AddProfile(owner,oldPhone);var archived=await AddProfile(owner,oldPhone,false);var separate=await AddProfile(owner,"0968000000");var other=await Create();var outsider=await AddProfile(other,oldPhone);var orphan=await AddProfile(null,oldPhone);
        using(var r=await Send(root+"preview",new PatientPhonePreviewRequest(NewPhone())))check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous cannot preview or enumerate family profiles");
        foreach(var role in new[]{RoleCodes.Admin,RoleCodes.Dentist,RoleCodes.Assistant,RoleCodes.Receptionist}){var u=await Create(role);using var r=await Send(root+"preview",new PatientPhonePreviewRequest(NewPhone()),tokens.GenerateAccessToken(u));check(r.StatusCode==HttpStatusCode.Forbidden,"Staff cannot use patient self route: "+role);}
        foreach(var phone in new[]{"bad","0967000001\n","0１２３４５６７８９"}){using var r=await Send(root+"preview",new PatientPhonePreviewRequest(phone),ownerToken);check(r.StatusCode==HttpStatusCode.BadRequest,"Malformed new phone rejected before data access");}
        foreach(var phone in new[]{oldPhone,other.Phone}){using var r=await Send(root+"preview",new PatientPhonePreviewRequest(phone),ownerToken);check(r.StatusCode==HttpStatusCode.Conflict,"Unchanged or occupied login number unavailable");check(!(await r.Content.ReadAsStringAsync()).Contains(other.FullName),"Unavailable number does not disclose another account");}
        var phoneNext=NewPhone();var scope=await Preview(owner,phoneNext);check(scope.Profiles.Count==3&&scope.Profiles.Count(p=>p.WillUpdate)==2,"Preview includes exactly own profiles and two matching contacts");check(scope.Profiles.Any(p=>p.PatientId==archived.PatientId&&!p.IsActive&&p.WillUpdate),"Archived matching profile explicitly included");check(scope.Profiles.Single(p=>p.PatientId==separate.PatientId).WillUpdate==false,"Separate contact explicitly unchanged");check(scope.Profiles.All(p=>p.PatientId!=outsider.PatientId&&p.PatientId!=orphan.PatientId),"Preview excludes other account and unlinked profile");
        using(var r=await Send(root+"request",new PatientPhoneRequest(phoneNext,scope.ReviewStamp,false),ownerToken))check(r.StatusCode==HttpStatusCode.BadRequest,"Scope consent required before SMS");
        using(var r=await Send(root+"request",new PatientPhoneRequest(phoneNext,scope.ReviewStamp,true),tokens.GenerateAccessToken(other)))check(r.StatusCode==HttpStatusCode.Conflict,"Foreign family review stamp cannot request OTP");
        sms.IsConfigured=false;using(var r=await Send(root+"request",new PatientPhoneRequest(phoneNext,scope.ReviewStamp,true),ownerToken))check(r.StatusCode==HttpStatusCode.ServiceUnavailable,"Unconfigured SMS fails closed");sms.IsConfigured=true;
        var pending=await Issue(owner,scope);await db.Entry(owner).ReloadAsync();check(owner.Phone==oldPhone&&owner.PhoneVerifiedAtUtc is null&&owner.TokenVersion==0&&owner.PasswordHash==hash,"Pending keeps old login, NULL verification, password and version");check(await Session(ownerToken),"Old session stays valid before confirmation");check(await db.Patients.CountAsync(p=>p.UserId==owner.UserId&&p.Phone==oldPhone)==2,"Pending does not update any patient contact");
        using(var r=await Send(root+"request",new PatientPhoneRequest(phoneNext,scope.ReviewStamp,true),ownerToken))check(r.StatusCode==HttpStatusCode.TooManyRequests,"Per-user resend cooldown enforced");
        using(var r=await Confirm(other,scope,pending))check(r.StatusCode==HttpStatusCode.BadRequest,"Another owner cannot consume family OTP");
        using(var r=await Send(root+"confirm",new PatientPhoneConfirmation(pending.ChallengeId,sms.Codes[phoneNext],new string('B',64),true),ownerToken))check(r.StatusCode==HttpStatusCode.BadRequest,"Correct OTP cannot confirm a different review stamp");
        using(var r=await Send(root+"confirm",new PatientPhoneConfirmation(pending.ChallengeId,sms.Codes[phoneNext],scope.ReviewStamp,false),ownerToken))check(r.StatusCode==HttpStatusCode.BadRequest,"Final consent required before OTP consumption");
        using(var r=await Send(root+"confirm",new PatientPhoneConfirmation(pending.ChallengeId,sms.Codes[phoneNext]+"\n",scope.ReviewStamp,true),ownerToken))check(r.StatusCode==HttpStatusCode.BadRequest,"Malformed OTP cannot consume valid proof");
        using(var r=await Confirm(owner,scope,pending,token:ownerToken)){check(r.StatusCode==HttpStatusCode.OK,"Owner confirms reviewed family change");var done=(await r.Content.ReadFromJsonAsync<PatientPhoneCompleted>())!;check(done.Completed&&done.UpdatedProfiles==2,"Completion reports exact confirmed scope count");}
        await db.Entry(owner).ReloadAsync();check(owner.Phone==phoneNext&&owner.PhoneVerifiedAtUtc.HasValue&&owner.TokenVersion==1&&owner.PasswordHash==hash,"Login number and genuine verification commit with version, unchanged hash");
        foreach(var p in new[]{active,archived}){await db.Entry(p).ReloadAsync();check(p.Phone==phoneNext&&p.UserId==owner.UserId&&p.Address=="Synthetic unchanged"&&p.Email=="synthetic@example.invalid","Only matching owned contact changes, ownership and other fields retained");}
        foreach(var p in new[]{separate,outsider,orphan}){await db.Entry(p).ReloadAsync();check(p.Phone==(p.PatientId==separate.PatientId?"0968000000":oldPhone),"Separate, foreign and unlinked contacts remain unchanged");}
        check(!await Session(ownerToken),"Old owner sessions revoked after atomic commit");using(var r=await Send("/api/auth/login",new LoginRequest(phoneNext,"Legacy123")))check(r.IsSuccessStatusCode,"New login number works with same password");using(var r=await Send("/api/auth/login",new LoginRequest(oldPhone,"Legacy123")))check(r.StatusCode==HttpStatusCode.Unauthorized,"Old number no longer logs in");using(var r=await Confirm(owner,scope,pending))check(r.StatusCode==HttpStatusCode.BadRequest,"Consumed family proof cannot replay after fresh login");
        check(await db.AuditLogs.CountAsync(a=>a.UserId==owner.UserId&&a.Action=="MOD_PAT_CONTACT_PHONE_SYNCED")==2,"Each changed profile has committed audit");
        var audit=await db.AuditLogs.SingleAsync(a=>a.UserId==owner.UserId&&a.Action=="MOD_AUTH_PATIENT_PHONE_CHANGED");check(System.Text.Json.JsonDocument.Parse(audit.Detail!).RootElement.GetProperty("patientIds").EnumerateArray().Select(x=>x.GetInt32()).Order().SequenceEqual(new[]{active.PatientId,archived.PatientId}.Order())&&audit.Detail!.Contains("DL-334"),"Account audit records approved scope IDs and decision");
        var empty=await Create();var emptyScope=await Preview(empty,NewPhone());check(emptyScope.Profiles.Count==0,"Zero-profile preview is explicit");var emptyProof=await Issue(empty,emptyScope);using(var r=await Confirm(empty,emptyScope,emptyProof)){check(r.IsSuccessStatusCode,"Empty scope may change only login number");check((await r.Content.ReadFromJsonAsync<PatientPhoneCompleted>())!.UpdatedProfiles==0,"Empty scope updates zero profiles");}
        var rollback=await Create();var rollbackPhone=rollback.Phone;var rollbackProfile=await AddProfile(rollback,rollback.Phone);var rollbackScope=await Preview(rollback,NewPhone());var rollbackProof=await Issue(rollback,rollbackScope);failure.Enabled=true;using(var r=await Confirm(rollback,rollbackScope,rollbackProof))check(r.StatusCode==HttpStatusCode.InternalServerError,"Required profile audit failure aborts all changes");failure.Enabled=false;await db.Entry(rollback).ReloadAsync();await db.Entry(rollbackProfile).ReloadAsync();check(rollback.Phone==rollbackPhone&&rollback.PhoneVerifiedAtUtc is null&&rollback.TokenVersion==0&&rollback.PasswordHash==hash&&rollbackProfile.Phone==rollbackPhone,"Rollback preserves User, Patient, verification and password atomically");check(!await db.AuditLogs.AnyAsync(a=>a.UserId==rollback.UserId&&a.Action=="MOD_PAT_CONTACT_PHONE_SYNCED"),"Failed transaction commits no contact audits");using(var r=await Confirm(rollback,rollbackScope,rollbackProof))check(r.StatusCode==HttpStatusCode.BadRequest,"Failed transaction proof cannot replay");
        foreach(var change in new[]{"contact","membership","add","name","archive","password","role","lock","version"})
        {var u=await Create();var p=await AddProfile(u,u.Phone);var scopeBefore=await Preview(u,NewPhone());var proof=await Issue(u,scopeBefore);
            if(change=="contact")await db.Patients.Where(x=>x.PatientId==p.PatientId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Phone,"0968000000"));
            else if(change=="membership")await db.Patients.Where(x=>x.PatientId==p.PatientId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.UserId,(int?)null));
            else if(change=="add")await AddProfile(u,u.Phone);
            else if(change=="name")await db.Patients.Where(x=>x.PatientId==p.PatientId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.FullName,"Synthetic updated"));
            else if(change=="archive")await db.Patients.Where(x=>x.PatientId==p.PatientId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false));
            else if(change=="password")await db.Users.Where(x=>x.UserId==u.UserId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.PasswordHash,"synthetic-new-hash"));
            else if(change=="role")await db.Users.Where(x=>x.UserId==u.UserId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.RoleId,roles[RoleCodes.Dentist].RoleId));
            else if(change=="lock")await db.Users.Where(x=>x.UserId==u.UserId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsActive,false));
            else await db.Users.Where(x=>x.UserId==u.UserId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.TokenVersion,1));
            using var r=await Confirm(u,scopeBefore,proof);check(!r.IsSuccessStatusCode,"Changed "+change+" invalidates reviewed family proof");check(!await db.Users.AnyAsync(x=>x.UserId==u.UserId&&x.Phone==scopeBefore.NewPhone),"Stale scope never changes login");}
        var stale=await Create();var staleMember=await AddProfile(stale,stale.Phone);var staleScope=await Preview(stale,NewPhone());await db.Patients.Where(p=>p.PatientId==staleMember.PatientId).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.Phone,"0968000000"));var sent=sms.Codes.Count;using(var r=await Send(root+"request",new PatientPhoneRequest(staleScope.NewPhone,staleScope.ReviewStamp,true),tokens.GenerateAccessToken(stale)))check(r.StatusCode==HttpStatusCode.Conflict,"Review changed before request rejects before SMS");check(sms.Codes.Count==sent,"Stale preview sends no SMS");
        var wrongScope=await Preview(stale,NewPhone());var wrongProof=await Issue(stale,wrongScope);var right=sms.Codes[wrongScope.NewPhone];for(var n=0;n<5;n++){using var r=await Confirm(stale,wrongScope,wrongProof,right=="000000"?"111111":"000000");check(r.StatusCode==HttpStatusCode.BadRequest,"Wrong family OTP rejected");}using(var r=await Confirm(stale,wrongScope,wrongProof))check(r.StatusCode==HttpStatusCode.BadRequest,"Fifth wrong attempt invalidates correct code too");
        var expiryScope=await Preview(stale,NewPhone());var expiryProof=await Issue(stale,expiryScope);clock.Now+=TimeSpan.FromMinutes(5);using(var r=await Confirm(stale,expiryScope,expiryProof))check(r.StatusCode==HttpStatusCode.BadRequest,"Family code expires exactly at five minutes");
        var a=await Create();await AddProfile(a,a.Phone);var z=await Create();await AddProfile(z,z.Phone);var shared=NewPhone();var sa=await Preview(a,shared);var sz=await Preview(z,shared);var pa=await Issue(a,sa);var ca=sms.Codes[shared];var pz=await Issue(z,sz);var races=await Task.WhenAll(Confirm(a,sa,pa,ca),Confirm(z,sz,pz));check(races.Count(x=>x.StatusCode==HttpStatusCode.OK)==1&&races.Count(x=>x.StatusCode==HttpStatusCode.Conflict)==1,"Concurrent families for one login number have exactly one winner");foreach(var response in races)response.Dispose();check(await db.Users.CountAsync(u=>u.Phone==shared)==1,"Unique login number preserved after race");
        var blocked=await Create(pending:true);using(var r=await Send(root+"preview",new PatientPhonePreviewRequest(NewPhone()),tokens.GenerateAccessToken(blocked)))check(r.StatusCode==HttpStatusCode.Unauthorized,"Pending activation cannot use signed JWT for phone change");
        fixedIp=true;for(var n=0;n<10;n++){using var r=await Send(root+"confirm",new PatientPhoneConfirmation(Guid.NewGuid(),"123456",new string('A',64),true),tokens.GenerateAccessToken(stale));check(r.StatusCode==HttpStatusCode.BadRequest,"Confirmation attempts bounded before rate limit");}using(var r=await Send(root+"confirm",new PatientPhoneConfirmation(Guid.NewGuid(),"123456",new string('A',64),true),tokens.GenerateAccessToken(stale)))check(r.StatusCode==HttpStatusCode.TooManyRequests&&r.Headers.CacheControl?.NoStore==true,"Confirmation IP rate limit returns NoStore");fixedIp=false;
        check(await db.AuditLogs.Where(a=>a.Action=="MOD_PAT_CONTACT_PHONE_SYNCED"||a.Action.StartsWith("MOD_AUTH_PATIENT_PHONE_")).AllAsync(a=>a.Detail!=null&&!a.Detail.Contains("0968")&&!a.Detail.Contains("Legacy123")&&!a.Detail.Contains("reviewStamp")&&!a.Detail.Contains("PasswordHash")),"Scope audits contain IDs and decisions, never phone, OTP or credential stamps");
        await app.StopAsync();
    }
}
