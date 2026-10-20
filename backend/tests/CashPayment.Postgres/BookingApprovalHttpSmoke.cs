using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Application.Common;
using Dental.Application.Features.MOD_APP;
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

internal static class BookingApprovalHttpSmoke
{
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public bool Fail;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Fail && data.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action == "MOD_APP_REQUEST_APPROVED")) throw new InvalidOperationException("Synthetic approval audit failure");
            return base.SavingChangesAsync(data,result,ct);
        }
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool,string> check)
    {
        var failure = new FailAudit();
        var dbOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure).Options;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args=[], EnvironmentName="Testing" });
        builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
        var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));builder.Configuration["Jwt:Secret"]=secret;
        builder.Services.AddScoped(_=>new DentalDbContext(dbOptions));builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<ICurrentUser,CurrentUser>();
        builder.Services.AddSingleton(TimeProvider.System);builder.Services.AddSingleton<VietnamClock>();builder.Services.AddScoped<IPasswordHasher,BCryptPasswordHasher>();
        builder.Services.AddScoped<IBookingApprovalRepository,BookingApprovalRepository>();builder.Services.AddScoped<BookingApprovalService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(BookingApprovalController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt=>{
            jwt.TokenValidationParameters=new TokenValidationParameters{ValidateIssuer=true,ValidIssuer="DentalClinic",ValidateAudience=true,ValidAudience="DentalClinicClient",IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),ClockSkew=TimeSpan.Zero};
            jwt.Events=new JwtBearerEvents{OnTokenValidated=AccessTokenVersionValidation.ValidateAsync};
        });
        builder.Services.AddAuthorization(x=>x.AddApplicationPolicies());
        await using var app=builder.Build();app.Use(async(c,next)=>{try{await next(c);}catch{c.Response.StatusCode=500;}});
        app.UseAuthentication();app.UseAuthorization();app.MapControllers();await app.StartAsync();
        using var client=new HttpClient{BaseAddress=new Uri(app.Urls.Single())};await using var db=new DentalDbContext(dbOptions);
        var tokenService=new JwtTokenService(builder.Configuration);var users=new Dictionary<string,User>();var clock=new VietnamClock(TimeProvider.System);var date=clock.Today.AddDays(3);var sequence=0;
        foreach(var role in new[]{RoleCodes.Admin,RoleCodes.Receptionist,RoleCodes.Patient,RoleCodes.Dentist,RoleCodes.Assistant})
        {
            var r=new Role{RoleCode=role,RoleName="Synthetic "+role};var u=new User{Role=r,FullName="Synthetic "+role,Phone="09681000"+(++sequence).ToString("D2"),PasswordHash=BCrypt.Net.BCrypt.HashPassword("Synthetic password",4)};db.Users.Add(u);users.Add(role,u);
        }
        await db.SaveChangesAsync();
        async Task<HttpResponseMessage> Send(string method,string url,object? body=null,string? role=RoleCodes.Admin,string? token=null)
        {
            using var request=new HttpRequestMessage(new HttpMethod(method),url);if(token is not null)request.Headers.Authorization=new("Bearer",token);else if(role is not null)request.Headers.Authorization=new("Bearer",tokenService.GenerateAccessToken(users[role]));
            if(body is not null)request.Content=JsonContent.Create(body);return await client.SendAsync(request);
        }
        async Task<BookingRequest> Seed(string? phone=null,int? patientId=null,int? actor=null,int? dentist=null,DateOnly? day=null)
        {
            var row=new BookingRequest{ClientRequestId=Guid.NewGuid(),IntentDigest=new string('A',64),FullName="Synthetic guest "+(++sequence),Phone=phone??("096820"+sequence.ToString("D4")),DateOfBirth=new(1990,1,1),Gender="Other",AppointmentDate=day??date.AddDays(sequence),SlotTime=new(8,30),SlotDurationMinutes=30,VerifiedPatientId=patientId,SubmittedByUserId=actor,DentistId=dentist};db.BookingRequests.Add(row);await db.SaveChangesAsync();return row;
        }
        async Task<BookingApprovalContext> Read(int id,string role=RoleCodes.Admin)
        {using var res=await Send("GET",$"/api/appointment-requests/{id}/approval",role:role);check(res.StatusCode==HttpStatusCode.OK&&res.Headers.CacheControl?.NoStore==true,"Approval context JWT and NoStore");return(await res.Content.ReadFromJsonAsync<BookingApprovalContext>())!;}
        ApproveBookingRequest Body(BookingApprovalContext c,int? patient=null,bool counter=false)=>new(c.Version,c.ReviewStamp,patient,!patient.HasValue,counter,true);
        async Task<BookingApprovalResult> Approve(int id,ApproveBookingRequest body,string role=RoleCodes.Admin)
        {using var res=await Send("POST",$"/api/appointment-requests/{id}/approve",body,role);check(res.StatusCode==HttpStatusCode.OK&&res.Headers.CacheControl?.NoStore==true,"Approval succeeds with NoStore");return(await res.Content.ReadFromJsonAsync<BookingApprovalResult>())!;}
        try
        {
            var fresh=await Seed();
            foreach(var role in new string?[]{null,RoleCodes.Patient,RoleCodes.Dentist,RoleCodes.Assistant})
            {using var read=await Send("GET",$"/api/appointment-requests/{fresh.Id}/approval",role:role);check(read.StatusCode==(role is null?HttpStatusCode.Unauthorized:HttpStatusCode.Forbidden),"Approval context role "+(role??"anonymous"));using var post=await Send("POST",$"/api/appointment-requests/{fresh.Id}/approve",new ApproveBookingRequest(1,new string('A',64),null,true,true,true),role);check(post.StatusCode==(role is null?HttpStatusCode.Unauthorized:HttpStatusCode.Forbidden),"Approval write role "+(role??"anonymous"));}
            var c=await Read(fresh.Id);check(!c.RequiresCounterConfirmation&&c.Account is null,"Entirely new guest can be reviewed without linking an existing family");
            using(var bad=await Send("POST",$"/api/appointment-requests/{fresh.Id}/approve",Body(c) with{Confirmed=false}))check(bad.StatusCode==HttpStatusCode.BadRequest,"Approval requires explicit confirmation");
            var beforeCount=await new AppointmentRepository(db).GetSlotBookingCountAsync(fresh.AppointmentDate,fresh.SlotTime);
            var result=await Approve(fresh.Id,Body(c));db.ChangeTracker.Clear();
            var saved=await db.BookingRequests.SingleAsync(x=>x.Id==fresh.Id);var patient=await db.Patients.SingleAsync(p=>p.PatientId==result.PatientId);var createdUser=await db.Users.Include(u=>u.Role).SingleAsync(u=>u.UserId==patient.UserId);
            check(saved.Status=="Approved"&&saved.Version==2&&saved.ApprovedAppointmentId==result.AppointmentId,"Approved request records appointment and new version");
            check(result.CreatedAccount&&result.RequiresPhoneActivation&&createdUser.RequiresPhoneActivation&&BCrypt.Net.BCrypt.Verify(fresh.Phone,createdUser.PasswordHash),"New account uses bcrypt default and mandatory first OTP");
            check(await new AppointmentRepository(db).GetSlotBookingCountAsync(fresh.AppointmentDate,fresh.SlotTime)==beforeCount,"Approval replaces hold without double counting capacity");
            using(var jwt=await Send("GET",$"/api/appointment-requests/{fresh.Id}/approval",token:tokenService.GenerateAccessToken(createdUser)))check(jwt.StatusCode==HttpStatusCode.Unauthorized,"JWT cannot authorize a new account before first OTP");
            var reconciled=await Read(fresh.Id);check(reconciled.Result?.AppointmentId==result.AppointmentId,"Read reconciles a completed write without resubmission");
            using(var repeat=await Send("POST",$"/api/appointment-requests/{fresh.Id}/approve",Body(c)))check(repeat.StatusCode==HttpStatusCode.Conflict,"Repeated approval never creates a second account or appointment");
            var family=users[RoleCodes.Patient];var familyHash=family.PasswordHash;var familyRequest=await Seed(family.Phone);c=await Read(familyRequest.Id);
            check(c.RequiresCounterConfirmation&&c.Account?.UserId==family.UserId,"Existing family phone requires in-person reconciliation for new member");
            var userCount=await db.Users.CountAsync();var patientCount=await db.Patients.CountAsync();
            using(var remote=await Send("POST",$"/api/appointment-requests/{familyRequest.Id}/approve",Body(c)))check(remote.StatusCode==HttpStatusCode.BadRequest,"OTP alone cannot approve a new family member remotely");
            check(await db.BookingRequests.AnyAsync(x=>x.Id==familyRequest.Id&&x.Status=="Pending")&&await db.Users.CountAsync()==userCount&&await db.Patients.CountAsync()==patientCount,"Blocked remote approval preserves hold and all identities");
            var familyResult=await Approve(familyRequest.Id,Body(c,counter:true),RoleCodes.Receptionist);
            check(!familyResult.CreatedAccount&&await db.Users.CountAsync()==userCount&&await db.Patients.AnyAsync(p=>p.PatientId==familyResult.PatientId&&p.UserId==family.UserId),"Receptionist counter confirmation adds one member to the existing family");
            check((await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==family.UserId)).PasswordHash==familyHash,"Existing account password is never reset");
            var old=new Patient{FullName="Synthetic old patient",Phone=family.Phone,DateOfBirth=new(1980,2,2),Gender="Other"};db.Patients.Add(old);await db.SaveChangesAsync();
            var oldRequest=await Seed(family.Phone);oldRequest.FullName=old.FullName;oldRequest.DateOfBirth=old.DateOfBirth;await db.SaveChangesAsync();c=await Read(oldRequest.Id);
            check(c.Patients.Any(p=>p.PatientId==old.PatientId&&p.CanSelect),"Staff receives matching unowned old profile for counter selection");
            using(var remote=await Send("POST",$"/api/appointment-requests/{oldRequest.Id}/approve",Body(c,old.PatientId)))check(remote.StatusCode==HttpStatusCode.BadRequest,"Old profile linking still requires counter confirmation DL325");
            var linked=await Approve(oldRequest.Id,Body(c,old.PatientId,true));check(linked.PatientId==old.PatientId&&await db.Patients.AsNoTracking().AnyAsync(p=>p.PatientId==old.PatientId&&p.UserId==family.UserId),"Counter approval preserves and links the selected old profile");
            // Confirmed ownership never permits selecting another family member.
            var owned=await db.Patients.AsNoTracking().SingleAsync(p=>p.PatientId==familyResult.PatientId);
            var verified=await Seed(family.Phone,owned.PatientId,family.UserId);verified.FullName=owned.FullName;verified.DateOfBirth=owned.DateOfBirth;await db.SaveChangesAsync();c=await Read(verified.Id);
            check(!c.RequiresCounterConfirmation&&c.VerifiedPatientId==owned.PatientId,"Verified owned profile needs no new family link");
            using(var altered=await Send("POST",$"/api/appointment-requests/{verified.Id}/approve",Body(c,old.PatientId)))check(altered.StatusCode==HttpStatusCode.Conflict,"Verified patient cannot be replaced by another member");
            await Approve(verified.Id,Body(c,owned.PatientId));
            var stale=await Seed(family.Phone);c=await Read(stale.Id);var familyEntity=await db.Users.SingleAsync(u=>u.UserId==family.UserId);familyEntity.TokenVersion++;await db.SaveChangesAsync();
            using(var changed=await Send("POST",$"/api/appointment-requests/{stale.Id}/approve",Body(c,counter:true)))check(changed.StatusCode==HttpStatusCode.Conflict,"Changed account invalidates review fingerprint");
            var failing=await Seed();c=await Read(failing.Id);userCount=await db.Users.CountAsync();patientCount=await db.Patients.CountAsync();var appointmentCount=await db.Appointments.CountAsync();failure.Fail=true;
            using(var failed=await Send("POST",$"/api/appointment-requests/{failing.Id}/approve",Body(c)))check(failed.StatusCode==HttpStatusCode.InternalServerError,"Synthetic audit failure is not reported as success");failure.Fail=false;
            check(await db.Users.CountAsync()==userCount&&await db.Patients.CountAsync()==patientCount&&await db.Appointments.CountAsync()==appointmentCount&&await db.BookingRequests.AsNoTracking().AnyAsync(x=>x.Id==failing.Id&&x.Status=="Pending"&&x.Version==1),"Audit failure rolls back account, patient, appointment, version and hold atomically");
            var racing=await Seed();c=await Read(racing.Id);var race=await Task.WhenAll(Send("POST",$"/api/appointment-requests/{racing.Id}/approve",Body(c)),Send("POST",$"/api/appointment-requests/{racing.Id}/approve",Body(c),RoleCodes.Receptionist));check(race.Count(x=>x.StatusCode==HttpStatusCode.OK)==1&&race.Count(x=>x.StatusCode==HttpStatusCode.Conflict)==1,"Concurrent approvals have one winner");foreach(var res in race)res.Dispose();
            check(await db.AuditLogs.CountAsync(a=>a.EntityType=="BookingRequest"&&a.EntityId==racing.Id&&a.Action=="MOD_APP_REQUEST_APPROVED")==1,"Concurrent approvals create one audit");
            var versusReject=await Seed();c=await Read(versusReject.Id);
            async Task<Result<bool>> RejectRace(){await using var d=new DentalDbContext(dbOptions);return await new BookingRequestRepository(d,clock).RejectAsync(versusReject.Id,users[RoleCodes.Receptionist].UserId,RoleCodes.Receptionist,new RejectBookingRequest(1,"Synthetic counter decision",true),null,default);}
            var approvalTask=Send("POST",$"/api/appointment-requests/{versusReject.Id}/approve",Body(c));var rejectTask=RejectRace();await Task.WhenAll(approvalTask,rejectTask);using(var response=await approvalTask){var rejected=await rejectTask;check(response.StatusCode==HttpStatusCode.OK&&rejected.IsFailure||response.StatusCode==HttpStatusCode.Conflict&&rejected.IsSuccess,"Approve versus reject has exactly one final decision");}
            check(await db.AuditLogs.CountAsync(a=>a.EntityType=="BookingRequest"&&a.EntityId==versusReject.Id&&(a.Action=="MOD_APP_REQUEST_APPROVED"||a.Action=="MOD_APP_REQUEST_REJECTED"))==1,"Approve versus reject writes one decision audit");
            var duplicateDay=await Seed(family.Phone,owned.PatientId,family.UserId);duplicateDay.FullName=owned.FullName;duplicateDay.DateOfBirth=owned.DateOfBirth;await db.SaveChangesAsync();db.Appointments.Add(new Appointment{PatientId=owned.PatientId,AppointmentDate=duplicateDay.AppointmentDate,SlotTime=new(10,0),CreatedByUserId=users[RoleCodes.Admin].UserId});await db.SaveChangesAsync();c=await Read(duplicateDay.Id);
            using(var res=await Send("POST",$"/api/appointment-requests/{duplicateDay.Id}/approve",Body(c,owned.PatientId)))check(res.StatusCode==HttpStatusCode.BadRequest,"Selected patient cannot get a second Scheduled appointment on the same day");
            var profileChange=await Seed(family.Phone);profileChange.FullName=old.FullName;profileChange.DateOfBirth=old.DateOfBirth;await db.SaveChangesAsync();c=await Read(profileChange.Id);var oldTracked=await db.Patients.SingleAsync(p=>p.PatientId==old.PatientId);oldTracked.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync();
            using(var res=await Send("POST",$"/api/appointment-requests/{profileChange.Id}/approve",Body(c,old.PatientId,true)))check(res.StatusCode==HttpStatusCode.Conflict,"Changed selected profile invalidates staff review");
            var foreignUser=new User{FullName="Synthetic other family",Phone="0968999999",RoleId=family.RoleId,PasswordHash=familyHash};db.Users.Add(foreignUser);await db.SaveChangesAsync();var foreignProfile=new Patient{FullName="Synthetic foreign owned",Phone=family.Phone,DateOfBirth=new(1970,3,3),UserId=foreignUser.UserId};db.Patients.Add(foreignProfile);await db.SaveChangesAsync();var foreignRequest=await Seed(family.Phone);foreignRequest.FullName=foreignProfile.FullName;foreignRequest.DateOfBirth=foreignProfile.DateOfBirth;await db.SaveChangesAsync();c=await Read(foreignRequest.Id);
            check(c.Patients.Any(p=>p.PatientId==foreignProfile.PatientId&&!p.CanSelect),"Profile already owned by another account is not selectable");using(var res=await Send("POST",$"/api/appointment-requests/{foreignRequest.Id}/approve",Body(c,foreignProfile.PatientId,true)))check(res.StatusCode==HttpStatusCode.Conflict,"Approval cannot transfer an existing profile between accounts");
            var past=await Seed(day:clock.Today.AddDays(-1));c=await Read(past.Id);check(c.BlockReason is not null,"Past held interval cannot be confirmed as a new appointment");using(var res=await Send("POST",$"/api/appointment-requests/{past.Id}/approve",Body(c)))check(res.StatusCode==HttpStatusCode.Conflict,"Past approval leaves Pending for explicit rejection");
            // A full slot already includes the hold; confirmation must preserve the 100th place.
            var full=await Seed();for(var n=0;n<99;n++)db.Appointments.Add(new Appointment{PatientId=old.PatientId,AppointmentDate=full.AppointmentDate,SlotTime=full.SlotTime,CreatedByUserId=users[RoleCodes.Admin].UserId});await db.SaveChangesAsync();c=await Read(full.Id);await Approve(full.Id,Body(c));
            check(await new AppointmentRepository(db).GetSlotBookingCountAsync(full.AppointmentDate,full.SlotTime)==100,"99 Scheduled plus held place stays exactly100 after approval");
            var reserved=await Seed(dentist:users[RoleCodes.Dentist].UserId);db.Appointments.Add(new Appointment{PatientId=old.PatientId,AppointmentDate=reserved.AppointmentDate,SlotTime=reserved.SlotTime,DentistId=reserved.DentistId,IsEmergency=true,EmergencyReason="Synthetic emergency",CreatedByUserId=users[RoleCodes.Admin].UserId});await db.SaveChangesAsync();
            db.SystemConfigs.Add(new SystemConfig{Key="Appointment:DefaultSlotDurationMinutes",Value="60"});await db.SaveChangesAsync();c=await Read(reserved.Id);var preserved=await Approve(reserved.Id,Body(c));
            check(await db.Appointments.AnyAsync(a=>a.AppointmentId==preserved.AppointmentId&&a.SlotTime==reserved.SlotTime&&a.SlotDurationMinutes==30),"Old held duration survives new slot configuration and acknowledged emergency overlap");
            var locked=await Seed(family.Phone);familyEntity.IsActive=false;await db.SaveChangesAsync();c=await Read(locked.Id);check(c.BlockReason is not null,"Locked account blocks approval without reopening or duplicate User");familyEntity.IsActive=true;await db.SaveChangesAsync();
            var audit=await db.AuditLogs.SingleAsync(a=>a.EntityType=="BookingRequest"&&a.EntityId==familyRequest.Id&&a.Action=="MOD_APP_REQUEST_APPROVED");using(var doc=JsonDocument.Parse(audit.Detail!))check(doc.RootElement.GetProperty("confirmedAtCounter").GetBoolean()&&!audit.Detail!.Contains(family.Phone)&&!audit.Detail.Contains(familyRequest.FullName),"Audit records real counter confirmation and IDs without private identity");
            var receptionist=await db.Users.SingleAsync(u=>u.UserId==users[RoleCodes.Receptionist].UserId);var originalRole=receptionist.RoleId;receptionist.RoleId=users[RoleCodes.Assistant].RoleId;await db.SaveChangesAsync();using(var res=await Send("GET",$"/api/appointment-requests/{locked.Id}/approval",role:RoleCodes.Receptionist))check(res.StatusCode==HttpStatusCode.Forbidden,"Actual database role overrides stale staff claims");receptionist.RoleId=originalRole;receptionist.IsActive=false;await db.SaveChangesAsync();using(var res=await Send("POST",$"/api/appointment-requests/{locked.Id}/approve",Body(c,counter:true),RoleCodes.Receptionist))check(res.StatusCode==HttpStatusCode.Forbidden,"Inactive staff cannot approve with old JWT");
        }
        finally { await app.StopAsync(); }
    }
}
