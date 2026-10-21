using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Api.RateLimiting;
using Dental.Application.Features.MOD_IMG;
using Dental.Application.Features.MOD_APP;
using Dental.Application.Common;
using Dental.Application.Features.Appointments.DTOs;
using Dental.Application.Features.Appointments.Services;
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

internal static class BookingRequestHttpSmoke
{
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Fail && data.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action.StartsWith("MOD_APP_REQUEST_"))) throw new InvalidOperationException("Synthetic audit failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Delivery:IBookingOtpDelivery
    {
        public bool IsConfigured{get;set;}=true;
        public readonly System.Collections.Concurrent.ConcurrentDictionary<string,string> Codes=new();
        public Task<bool>SendAsync(string phone,string code,CancellationToken ct){Codes[phone]=code;return Task.FromResult(true);}
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
        var clock=new Clock();var delivery=new Delivery();
        builder.Services.AddSingleton<TimeProvider>(clock);builder.Services.AddSingleton<VietnamClock>();builder.Services.AddSingleton<IBookingOtpDelivery>(delivery);builder.Services.AddSingleton<IBookingOtp,BookingOtp>();
        builder.Services.AddScoped<IBookingRequestRepository,BookingRequestRepository>();builder.Services.AddScoped<BookingRequestService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(DentalImagesController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt => { jwt.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "DentalClinic", ValidateAudience = true, ValidAudience = "DentalClinicClient", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), ClockSkew = TimeSpan.Zero }; jwt.Events = new JwtBearerEvents { OnTokenValidated = AccessTokenVersionValidation.ValidateAsync }; });
        builder.Services.AddAuthorization(x => x.AddApplicationPolicies());
        builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; DentalImageRateLimits.Configure(o); BookingRequestRateLimits.Configure(o); });
        await using var app = builder.Build(); app.Use(async (c, next) => { try { await next(c); } catch { c.Response.StatusCode = 500; } });
        app.Use((c,next)=>{if(System.Net.IPAddress.TryParse(c.Request.Headers["X-Test-Client-IP"],out var ip))c.Connection.RemoteIpAddress=ip;return next();});
        app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var tokens = new JwtTokenService(builder.Configuration); await using var db = new DentalDbContext(dbOptions);
        var users = new Dictionary<string, User>();
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Receptionist, RoleCodes.Patient })
        { var user = new User { Role = await db.Roles.FirstOrDefaultAsync(r=>r.RoleCode==role) ?? new Role {RoleCode=role,RoleName="Synthetic "+role}, FullName = "Synthetic image account", Phone = "096800000" + users.Count, PasswordHash = "synthetic-not-used" }; users[role] = user; db.Users.Add(user); }
        await db.SaveChangesAsync();
        var vn=new VietnamClock(clock);var day=vn.Today.AddDays(3);var phoneSerial=100;var ipSerial=1;
        async Task<HttpResponseMessage> Send(string method,string path,object? body=null,string? role=null,string? ip=null){using var req=new HttpRequestMessage(new HttpMethod(method),path);req.Headers.Add("X-Test-Client-IP",ip??$"10.1.{ipSerial/250}.{ipSerial++%250+1}");if(role is not null)req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",tokens.GenerateAccessToken(users[role]));if(body is not null)req.Content=JsonContent.Create(body);return await client.SendAsync(req);}
        using var catalogResponse=await Send("GET","/api/appointment-requests/catalog");check(catalogResponse.StatusCode==HttpStatusCode.OK&&catalogResponse.Headers.CacheControl?.NoStore==true,"Public catalog has NoStore");
        var catalog=(await catalogResponse.Content.ReadFromJsonAsync<PublicBookingCatalog>())!;var rawCatalog=await catalogResponse.Content.ReadAsStringAsync();check(catalog.Dentists.Length==1&&!rawCatalog.Contains("phone",StringComparison.OrdinalIgnoreCase)&&!rawCatalog.Contains("patient",StringComparison.OrdinalIgnoreCase),"Catalog exposes only professional names and schedule, no patient/contact data");
        BookingIntent Intent(DateOnly? date=null,int? dentist=null)=>new("Synthetic guest "+phoneSerial,"0969"+(phoneSerial++).ToString("D6"),new(1990,1,1),"Other",null,date??day,new(8,0),dentist,"Synthetic request note",catalog.Revision);
        async Task<SubmitBookingRequest> Prepare(BookingIntent intent,string? role=null){clock.Now+=TimeSpan.FromSeconds(61);using var res=await Send("POST","/api/appointment-requests/otp",new BookingOtpRequest(intent),role);check(res.StatusCode==HttpStatusCode.Accepted&&res.Headers.CacheControl?.NoStore==true,"Issue bound OTP/NoStore");var c=(await res.Content.ReadFromJsonAsync<BookingOtpResponse>())!;return new(intent,c.ChallengeId,delivery.Codes[intent.Phone],Guid.NewGuid(),true);}
        async Task<int> Saved(Guid id){await using var d=new DentalDbContext(dbOptions);return await d.BookingRequests.Where(x=>x.ClientRequestId==id).Select(x=>x.Id).SingleAsync();}
        async Task<HttpResponseMessage> Submit(SubmitBookingRequest req,string? role=null)=>await Send("POST","/api/appointment-requests",req,role);
        async Task<BookingRequestPageLocal> List(){using var res=await Send("GET","/api/appointment-requests",role:RoleCodes.Receptionist);check(res.StatusCode==HttpStatusCode.OK&&res.Headers.CacheControl?.NoStore==true,"Receptionist list JWT/NoStore");return(await res.Content.ReadFromJsonAsync<BookingRequestPageLocal>())!;}
        async Task<Patient> Patient(string name){var p=new Patient{FullName=name,Phone=users[RoleCodes.Patient].Phone,DateOfBirth=new(1990,1,1),Gender="Other",UserId=users[RoleCodes.Patient].UserId};db.Patients.Add(p);await db.SaveChangesAsync();return p;}
        Patient returning=await Patient("Synthetic returning"),unseen=await Patient("Synthetic family newcomer");db.Visits.Add(new Visit{PatientId=returning.PatientId,CreatedByUserId=users[RoleCodes.Admin].UserId,Status=VisitStatuses.Completed,IsLocked=true});await db.SaveChangesAsync();
        var initialUsers=await db.Users.CountAsync();var initialPatients=await db.Patients.CountAsync();
        try{
            foreach(var role in new string?[]{null,RoleCodes.Patient,RoleCodes.Dentist,RoleCodes.Assistant}){using var res=await Send("GET","/api/appointment-requests",role:role);check(res.StatusCode==(role is null?HttpStatusCode.Unauthorized:HttpStatusCode.Forbidden),"Private review policy "+(role??"anonymous"));}
            using(var bad=await Send("POST","/api/appointment-requests/otp",new BookingOtpRequest(Intent() with{PatientId=returning.PatientId})))check(bad.StatusCode==HttpStatusCode.NotFound,"Guest cannot claim another patient by ID");
            var plain=Intent();var prepared=await Prepare(plain);using(var changed=await Submit(prepared with{Intent=plain with{SlotTime=new(8,30)}}))check(changed.StatusCode==HttpStatusCode.BadRequest,"OTP cannot authorize edited intent");
            using(var ok=await Submit(prepared)){check(ok.StatusCode==HttpStatusCode.Accepted&&ok.Headers.CacheControl?.NoStore==true,"Guest submission accepts Pending/NoStore");var receipt=await ok.Content.ReadAsStringAsync();check(!receipt.Contains(plain.Phone)&&!receipt.Contains(plain.FullName)&&!receipt.Contains("returning",StringComparison.OrdinalIgnoreCase),"Receipt does not disclose personal data or priority");}
            var firstId=await Saved(prepared.ClientRequestId);using(var replay=await Submit(prepared))check(replay.StatusCode==HttpStatusCode.Accepted,"Exact lost-response replay succeeds without another OTP");
            check(await db.BookingRequests.CountAsync(x=>x.ClientRequestId==prepared.ClientRequestId)==1&&await db.AuditLogs.CountAsync(x=>x.EntityId==firstId&&x.Action=="MOD_APP_REQUEST_SUBMITTED")==1,"Replay creates one request and one audit");
            check(await db.Users.CountAsync()==initialUsers&&await db.Patients.CountAsync()==initialPatients,"Pending creates no User or Patient");
            var own=Intent() with{FullName=returning.FullName,Phone=users[RoleCodes.Patient].Phone,PatientId=returning.PatientId};var ownReq=await Prepare(own,RoleCodes.Patient);using(var res=await Submit(ownReq,RoleCodes.Patient))check(res.StatusCode==HttpStatusCode.Accepted,"Owned Completed profile can submit verified request");
            var child=Intent() with{FullName=unseen.FullName,Phone=users[RoleCodes.Patient].Phone,PatientId=unseen.PatientId};var childReq=await Prepare(child,RoleCodes.Patient);using(var res=await Submit(childReq,RoleCodes.Patient))check(res.StatusCode==HttpStatusCode.Accepted,"New family member can submit without inheriting priority");
            var list=await List();check(list.Items[0].Id==await Saved(ownReq.ClientRequestId)&&list.Items[0].IsReturning,"Actual Completed profile sorts ahead of older guest request");check(!list.Items.Single(x=>x.Id==awaitId(childReq.ClientRequestId)).IsReturning,"Family account alone grants no priority");
            int awaitId(Guid id)=>db.BookingRequests.AsNoTracking().Single(x=>x.ClientRequestId==id).Id;
            await using(var d=new DentalDbContext(dbOptions)){var repository=new AppointmentRepository(d);check(await repository.HasActiveAppointmentOnDateAsync(returning.PatientId,day),"Legacy same-patient/day guard sees verified Pending");check(await repository.GetSlotBookingCountAsync(day,new(8,0))==3,"Pending count joins legacy slot occupancy");}
            failure.Fail=true;using(var res=await Send("POST",$"/api/appointment-requests/{firstId}/reject",new RejectBookingRequest(1,"Synthetic audit rollback",true),RoleCodes.Admin))check(res.StatusCode==HttpStatusCode.InternalServerError,"Reject audit failure propagated");failure.Fail=false;
            await using(var d=new DentalDbContext(dbOptions))check(await d.BookingRequests.AnyAsync(x=>x.Id==firstId&&x.Status=="Pending"&&x.Version==1),"Reject audit failure preserves original hold and version");
            var rejects=await Task.WhenAll(Send("POST",$"/api/appointment-requests/{firstId}/reject",new RejectBookingRequest(1,"Synthetic race",true),RoleCodes.Admin),Send("POST",$"/api/appointment-requests/{firstId}/reject",new RejectBookingRequest(1,"Synthetic race",true),RoleCodes.Receptionist));check(rejects.Count(x=>x.StatusCode==HttpStatusCode.NoContent)==1&&rejects.Count(x=>x.StatusCode==HttpStatusCode.Conflict)==1,"Concurrent rejection has one winner");foreach(var res in rejects)res.Dispose();
            var held=Intent(day.AddDays(1),users[RoleCodes.Dentist].UserId);var heldReq=await Prepare(held);using(var res=await Submit(heldReq))check(res.StatusCode==HttpStatusCode.Accepted,"Pending holds selected dentist");
            await using(var d=new DentalDbContext(dbOptions)){var repo=new AppointmentRepository(d);check(await repo.HasDentistBookingAsync(users[RoleCodes.Dentist].UserId,held.AppointmentDate,held.SlotTime,0),"Legacy dentist guard sees Pending");var hit=await repo.GetPendingDentistConflictAsync(users[RoleCodes.Dentist].UserId,held.AppointmentDate,held.SlotTime,30);check(hit?.RequestId==await Saved(heldReq.ClientRequestId),"Pending conflict has distinct positive request ID");}
            var clash=await Prepare(Intent(held.AppointmentDate,users[RoleCodes.Dentist].UserId));using(var res=await Submit(clash))check(res.StatusCode==HttpStatusCode.Conflict,"Second request cannot take held dentist slot");
            var holderId=await Saved(heldReq.ClientRequestId);
            async Task<Result<AppointmentResponse>> Legacy(BookingIntent i,bool emergency=false,int? ack=null){await using var d=new DentalDbContext(dbOptions);var svc=new AppointmentService(new AppointmentRepository(d),new PatientRepository(d),new UserRepository(d),new UnitOfWork(d),new AuditLogger(d,Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditLogger>.Instance),vn);return await svc.CreateAppointmentAsync(users[RoleCodes.Admin].UserId,RoleCodes.Admin,new CreateAppointmentRequest(returning.PatientId,i.AppointmentDate,i.SlotTime,i.DentistId,IsEmergency:emergency,EmergencyReason:emergency?"Synthetic urgent visit":null,ExpectedScheduleRevision:catalog.Revision,ConfirmedDentistConflictRequestId:ack),null);}
            check((await Legacy(held)).IsFailure,"Existing counter booking cannot take a Pending dentist slot");
            var rejected=await Legacy(held,true);check(rejected.IsFailure&&rejected.Error.Code==Error.Validation.Code,"Retired emergency cannot bypass Pending hold");
            var acknowledged=await Legacy(held,true,holderId);check(acknowledged.IsFailure&&acknowledged.Error.Code==Error.Validation.Code,"Legacy acknowledgement cannot bypass Pending hold");
            check(await db.BookingRequests.AnyAsync(x=>x.Id==holderId&&x.Status=="Pending"),"Rejected emergency preserves existing Pending hold");


            foreach(var role in new string?[]{null,RoleCodes.Patient,RoleCodes.Assistant,RoleCodes.Dentist}){using var res=await Send("POST",$"/api/appointment-requests/{holderId}/reject",new RejectBookingRequest(1,"Synthetic reason",true),role);check(res.StatusCode==(role is null?HttpStatusCode.Unauthorized:HttpStatusCode.Forbidden),"Reject policy "+(role??"anonymous"));}
            using(var res=await Send("POST",$"/api/appointment-requests/{holderId}/reject",new RejectBookingRequest(1,"Synthetic reason",false),RoleCodes.Admin))check(res.StatusCode==HttpStatusCode.BadRequest,"Reject requires explicit confirmation");
            using(var res=await Send("POST",$"/api/appointment-requests/{holderId}/reject",new RejectBookingRequest(2,"Synthetic reason",true),RoleCodes.Admin))check(res.StatusCode==HttpStatusCode.Conflict,"Stale rejection version blocked");
            using(var res=await Send("POST",$"/api/appointment-requests/{holderId}/reject",new RejectBookingRequest(1,"Synthetic private reason",true),RoleCodes.Receptionist))check(res.StatusCode==HttpStatusCode.NoContent&&res.Headers.CacheControl?.NoStore==true,"Receptionist rejects with NoStore");
            await using(var d=new DentalDbContext(dbOptions)){check(!await new AppointmentRepository(d).HasDentistBookingAsync(users[RoleCodes.Dentist].UserId,held.AppointmentDate,held.SlotTime,0),"Rejection releases dentist hold");var audit=await d.AuditLogs.SingleAsync(a=>a.EntityId==holderId&&a.Action=="MOD_APP_REQUEST_REJECTED");check(!audit.Detail!.Contains("Synthetic private reason")&&!audit.Detail.Contains(held.Phone),"Audit contains no free reason, name, phone or OTP");}
            using(var repeat=await Send("POST",$"/api/appointment-requests/{holderId}/reject",new RejectBookingRequest(1,"Synthetic reason",true),RoleCodes.Admin))check(repeat.StatusCode==HttpStatusCode.Conflict,"Repeat rejection cannot change record or audit twice");
            var retry=await Prepare(Intent(held.AppointmentDate,users[RoleCodes.Dentist].UserId));using(var res=await Submit(retry))check(res.StatusCode==HttpStatusCode.Accepted,"Released dentist slot accepts a new request");
            // DB failure must not leave a hold, patient or account behind; consumed OTP needs a fresh challenge.
            var failReq=await Prepare(Intent(day.AddDays(2)));failure.Fail=true;using(var res=await Submit(failReq))check(res.StatusCode==HttpStatusCode.InternalServerError,"Synthetic submission audit failure propagated");failure.Fail=false;check(!await db.BookingRequests.AnyAsync(x=>x.ClientRequestId==failReq.ClientRequestId),"Audit failure rolls back request/hold");
            var raceDay=day.AddDays(3);var a=await Prepare(Intent(raceDay,users[RoleCodes.Dentist].UserId));var b=await Prepare(Intent(raceDay,users[RoleCodes.Dentist].UserId));var results=await Task.WhenAll(Submit(a),Submit(b));check(results.Count(x=>x.StatusCode==HttpStatusCode.Accepted)==1&&results.All(x=>x.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.Conflict),"Concurrent requests have one dentist-slot winner");foreach(var res in results)res.Dispose();
            var crossIntent=Intent(day.AddDays(5),users[RoleCodes.Dentist].UserId);var crossRequest=await Prepare(crossIntent);var legacyTask=Legacy(crossIntent);var guestTask=Submit(crossRequest);await Task.WhenAll(legacyTask,guestTask);using(var res=await guestTask)check(((await legacyTask).IsSuccess?1:0)+(res.StatusCode==HttpStatusCode.Accepted?1:0)==1,"Old counter and new public request cannot both claim one dentist slot");
            var capDay=day.AddDays(4);for(var i=0;i<99;i++)db.Appointments.Add(new Appointment{PatientId=returning.PatientId,CreatedByUserId=users[RoleCodes.Admin].UserId,AppointmentDate=capDay,SlotTime=new(8,0),SlotDurationMinutes=30});await db.SaveChangesAsync();
            var last=await Prepare(Intent(capDay));using(var res=await Submit(last))check(res.StatusCode==HttpStatusCode.Accepted,"99 existing appointments allow one Pending as slot100");var over=await Prepare(Intent(capDay));using(var res=await Submit(over))check(res.StatusCode==HttpStatusCode.BadRequest,"101st combined reservation rejected");
            var capId=await Saved(last.ClientRequestId);using(var res=await Send("POST",$"/api/appointment-requests/{capId}/reject",new RejectBookingRequest(1,"Synthetic release",true),RoleCodes.Admin))check(res.StatusCode==HttpStatusCode.NoContent,"Admin rejection releases capacity");
            await using(var d=new DentalDbContext(dbOptions))check(await new AppointmentRepository(d).GetSlotBookingCountAsync(capDay,new(8,0))==99,"Released capacity is visible to existing booking flow");
            delivery.IsConfigured=false;using(var unavailable=await Send("POST","/api/appointment-requests/otp",new BookingOtpRequest(Intent())))check(unavailable.StatusCode==HttpStatusCode.ServiceUnavailable,"Missing delivery configuration fails clearly without fake success");delivery.IsConfigured=true;
            // Real configured IP limiter remains active; distinct test IPs above represent independent callers.
            var rateIntent=Intent();HttpStatusCode final=0;for(var n=0;n<11;n++){using var res=await Send("POST","/api/appointment-requests/otp",new BookingOtpRequest(Intent()),ip:"10.250.0.1");final=res.StatusCode;}check(final==HttpStatusCode.TooManyRequests,"OTP endpoint enforces IP rate limit");
            var receptionist=users[RoleCodes.Receptionist];var oldRole=receptionist.RoleId;receptionist.RoleId=users[RoleCodes.Assistant].RoleId;await db.SaveChangesAsync();using(var res=await Send("GET","/api/appointment-requests",role:RoleCodes.Receptionist))check(res.StatusCode==HttpStatusCode.Forbidden,"Actual DB role controls stale staff JWT");receptionist.RoleId=oldRole;await db.SaveChangesAsync();
            users[RoleCodes.Receptionist].IsActive=false;await db.SaveChangesAsync();using(var res=await Send("GET","/api/appointment-requests",role:RoleCodes.Receptionist))check(res.StatusCode==HttpStatusCode.Forbidden,"Inactive staff cannot read with old token");
        }finally{await app.StopAsync();if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    private sealed record BookingRequestPageLocal(BookingRequestRow[] Items,int TotalCount,int Page,int PageSize);
}
