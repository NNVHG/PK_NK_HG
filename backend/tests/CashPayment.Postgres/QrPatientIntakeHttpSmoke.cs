using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_PAT;
using Dental.Application.Features.Patients.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
internal static class QrPatientIntakeHttpSmoke
{
 public static async Task RunAsync(HttpClient client,DbContextOptions<DentalDbContext> options,int cashierId,Action<string,int> auth,Action<bool,string> check)
 {
  await using var db=new DentalDbContext(options);var clock=new VietnamClock(SmokeTestTimeProvider.Instance);
  if(!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_",StringComparison.Ordinal))throw new InvalidOperationException("Owned DB required");
  async Task<User> Actor(string code)
  {var role=await db.Roles.SingleOrDefaultAsync(r=>r.RoleCode==code);if(role is null){role=new(){RoleCode=code,RoleName=code};db.Roles.Add(role);await db.SaveChangesAsync();}var u=new User{RoleId=role.RoleId,FullName="Synthetic QR staff",Phone="QR"+Guid.NewGuid().ToString("N")[..12],PasswordHash=BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString())};db.Users.Add(u);await db.SaveChangesAsync();return u;}
  cashierId=(await Actor(RoleCodes.Receptionist)).UserId;
  var admin=await Actor(RoleCodes.Admin);var assistant=await Actor(RoleCodes.Assistant);var doctor=await Actor(RoleCodes.Dentist);var patient=await Actor(RoleCodes.Patient);
  const string lookup="/api/patients/check-cccd",create="/api/patients/qr-intake";
  var identityHasher=new KeyedIdentityHasher(new byte[32]);
  var request=new QrPatientIntakeRequest("987654311101",new("Synthetic QR patient",new(1990,1,1),"Male","0990011101",Address:"Synthetic address"),true);
  // Previous identity smoke ends with authentication; reset before anonymous checks.
  client.DefaultRequestHeaders.Authorization=null;
  using(var r=await client.PostAsJsonAsync(lookup,new QrIdentityLookupRequest(request.CitizenId)))check(r.StatusCode==HttpStatusCode.Unauthorized,"QR anonymous lookup401");
  using(var r=await client.PostAsJsonAsync(create,request))check(r.StatusCode==HttpStatusCode.Unauthorized,"QR anonymous create401");
  foreach(var actor in new[]{(RoleCodes.Assistant,assistant.UserId),(RoleCodes.Dentist,doctor.UserId),(RoleCodes.Patient,patient.UserId)})
  {auth(actor.Item1,actor.Item2);using(var r=await client.PostAsJsonAsync(lookup,new QrIdentityLookupRequest(request.CitizenId)))check(r.StatusCode==HttpStatusCode.Forbidden,"QR unsupported role lookup403");using(var r=await client.PostAsJsonAsync(create,request))check(r.StatusCode==HttpStatusCode.Forbidden,"QR unsupported role create403");}
  auth(RoleCodes.Receptionist,cashierId);
  foreach(var citizenId in new[]{"","98765431110",new string('G',12),new string('1',13)})
  {using var r=await client.PostAsJsonAsync(lookup,new QrIdentityLookupRequest(citizenId));check(r.StatusCode==HttpStatusCode.BadRequest,"QR malformed CCCD rejected400");}
  using(var r=await client.PostAsJsonAsync(lookup,new QrIdentityLookupRequest(request.CitizenId)))
  {var data=await r.Content.ReadFromJsonAsync<QrIdentityLookupResult>();check(r.StatusCode==HttpStatusCode.OK&&data!.Existing is null&&r.Headers.CacheControl?.NoStore==true,"QR no match honest null NoStore");}
  foreach(var invalid in new[]{request with {Confirmed=false},request with {CitizenId="98765431110A"},request with {Patient=request.Patient with {Phone="123"}},request with {Patient=request.Patient with {DateOfBirth=clock.Today.AddDays(1)}}})
  {using var r=await client.PostAsJsonAsync(create,invalid);check(r.StatusCode==HttpStatusCode.BadRequest,"QR invalid confirmation/identity/demographics400");}
  PatientResponse created;
  using(var r=await client.PostAsJsonAsync(create,request)){created=(await r.Content.ReadFromJsonAsync<PatientResponse>())!;check(r.StatusCode==HttpStatusCode.Created&&created.PatientId>0&&r.Headers.CacheControl?.NoStore==true,"QR cashier creates profile atomically201 NoStore");}
  var saved=await db.Patients.AsNoTracking().SingleAsync(p=>p.PatientId==created.PatientId);
  check(saved.CitizenIdHash==identityHasher.Hash(request.CitizenId)&&saved.CitizenIdMasked==identityHasher.Mask(request.CitizenId)&&saved.CitizenIdVersion==1,"QR stores server HMAC and server mask, never raw CCCD or SHA256");
  check(saved.DocumentType==IdentityDocumentType.CitizenId&&saved.IssuingCountryCode is null&&
    saved.HashScheme==IdentityHashScheme.HmacCanonical&&saved.IdentityKeyVersion==IdentityDocumentConstants.CurrentHmacKeyVersion,
    "QR profile records verified CCCD and HMAC version");
  check(!await db.QueueEntries.AnyAsync(q=>q.PatientId==created.PatientId)&&!await db.Visits.AnyAsync(v=>v.PatientId==created.PatientId)&&saved.UserId is null,"QR creation does not auto check-in or change portal ownership");
  using(var r=await client.PostAsJsonAsync(lookup,new QrIdentityLookupRequest(request.CitizenId)))
  {var data=await r.Content.ReadFromJsonAsync<QrIdentityLookupResult>();var text=await r.Content.ReadAsStringAsync();check(data!.Existing!.PatientId==created.PatientId&&!text.Contains(request.CitizenId)&&!text.Contains(saved.CitizenIdHash),"QR existing HMAC profile lookup returns no raw identity/hash");}
  using(var r=await client.PostAsJsonAsync(create,request with {Patient=request.Patient with {ConfirmNotDuplicate=true,FullName="Changed"}}))check(r.StatusCode==HttpStatusCode.Conflict,"QR identity duplicate cannot be overridden by shared-phone confirmation409");
  check(await db.Patients.CountAsync(p=>p.CitizenIdHash==identityHasher.Hash(request.CitizenId))==1&&await db.Patients.AnyAsync(p=>p.PatientId==created.PatientId&&p.FullName==request.Patient.FullName),"QR duplicate never rewrites existing demographics");
  var legacyPatient=new Patient{FullName="Synthetic legacy QR",Phone="0990011199",DateOfBirth=new DateOnly(1990,1,1),CitizenIdHash=CitizenIdIdentity.Hash("987654311199"),CitizenIdMasked="987******199",CreatedAt=DateTime.UtcNow};
  db.Patients.Add(legacyPatient);await db.SaveChangesAsync();
  check(legacyPatient.DocumentType is null&&legacyPatient.HashScheme is null&&legacyPatient.IdentityKeyVersion is null,
    "Legacy SHA256 profile remains unclassified before verified conversion");
  using(var r=await client.PostAsJsonAsync(lookup,new QrIdentityLookupRequest("987654311199")))
    check((await r.Content.ReadFromJsonAsync<QrIdentityLookupResult>())!.Existing?.PatientId==legacyPatient.PatientId,"QR lookup recognizes legacy SHA256 profile");
  using(var r=await client.PostAsJsonAsync(create,request with {CitizenId="987654311199"}))check(r.StatusCode==HttpStatusCode.Conflict,"QR cannot duplicate legacy SHA256 profile");
  var distinctPatient=request with {CitizenId="987654311102",Patient=request.Patient with {FullName="Synthetic distinctPatient member"}};
  using(var r=await client.PostAsJsonAsync(create,distinctPatient)){var data=await r.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();check(r.StatusCode==HttpStatusCode.Conflict&&data.GetProperty("code").GetString()=="PATIENT_POSSIBLE_DUPLICATE"&&data.GetProperty("duplicates").GetArrayLength()>0,"QR shared phone requires explicit distinctPatient confirmation409");}
  using(var r=await client.PostAsJsonAsync(create,distinctPatient with {Patient=distinctPatient.Patient with {ConfirmNotDuplicate=true}}))check(r.StatusCode==HttpStatusCode.Created,"QR confirmed distinct distinctPatient member with same phone201");
  var archive=await db.Patients.FindAsync(created.PatientId);archive!.IsActive=false;await db.SaveChangesAsync();db.ChangeTracker.Clear();
  using(var r=await client.PostAsJsonAsync(lookup,new QrIdentityLookupRequest(request.CitizenId)))check((await r.Content.ReadFromJsonAsync<QrIdentityLookupResult>())!.Existing is {IsActive:false},"QR archived identity still resolves and blocks duplicate");
  var audit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.Action=="MOD_PAT_QR_INTAKE_CREATED"&&a.EntityId==created.PatientId);
  check(audit.UserId==cashierId&&!audit.Detail!.Contains(request.CitizenId)&&!audit.Detail.Contains(saved.CitizenIdHash)&&!audit.Detail.Contains(request.Patient.FullName),"QR required audit excludes identity and demographic payload");
  var faultBody=request with {CitizenId="987654311103",Patient=request.Patient with {Phone="0990011103"}};
  var faultOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailQrAudit()).Options;var failed=false;
  await using(var ctx=new DentalDbContext(faultOptions))try{await new QrPatientIntakeRepository(ctx,clock,identityHasher).CreateAsync(cashierId,RoleCodes.Receptionist,faultBody,null,default);}catch(InvalidOperationException e)when(e.Message=="Synthetic QR audit fault"){failed=true;}
  check(failed&&!await db.Patients.AnyAsync(p=>p.CitizenIdHash==identityHasher.Hash(faultBody.CitizenId)),"QR audit failure rolls back profile and identity");
  var race=request with {CitizenId="987654311104",Patient=request.Patient with {Phone="0990011104"}};
  async Task<bool> Race(){await using var ctx=new DentalDbContext(options);return(await new QrPatientIntakeRepository(ctx,clock,identityHasher).CreateAsync(admin.UserId,RoleCodes.Admin,race,null,default)).IsSuccess;}
  check((await Task.WhenAll(Race(),Race())).Count(ok=>ok)==1&&await db.Patients.CountAsync(p=>p.CitizenIdHash==identityHasher.Hash(race.CitizenId))==1,"QR concurrent creates exactly one profile");
  auth(RoleCodes.Admin,admin.UserId);
  using(var r=await client.PostAsJsonAsync(lookup,new QrIdentityLookupRequest(request.CitizenId)))check(r.StatusCode==HttpStatusCode.OK,"QR Admin lookup200");
  using(var r=await client.PostAsJsonAsync(create,faultBody))check(r.StatusCode==HttpStatusCode.Created,"QR Admin creates new identity profile201");
  var inactive=await db.Users.FindAsync(admin.UserId);inactive!.IsActive=false;await db.SaveChangesAsync();db.ChangeTracker.Clear();auth(RoleCodes.Admin,admin.UserId);
  using(var r=await client.PostAsJsonAsync(lookup,new QrIdentityLookupRequest(request.CitizenId)))check(r.StatusCode==HttpStatusCode.Forbidden,"QR disabled actor stale JWT lookup403");
  using(var r=await client.PostAsJsonAsync(create,faultBody))check(r.StatusCode==HttpStatusCode.Forbidden,"QR disabled actor stale JWT create403");
 }
 private sealed class FailQrAudit:SaveChangesInterceptor
 {public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default){if(data.Context!.ChangeTracker.Entries<AuditLog>().Any(a=>a.Entity.Action=="MOD_PAT_QR_INTAKE_CREATED"))throw new InvalidOperationException("Synthetic QR audit fault");return ValueTask.FromResult(result);}}
}
