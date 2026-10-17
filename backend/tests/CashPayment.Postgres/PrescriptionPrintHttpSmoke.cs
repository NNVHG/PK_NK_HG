using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.Staff.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
internal static class PrescriptionPrintHttpSmoke
{
 public static async Task RunAsync(HttpClient client,DbContextOptions<DentalDbContext> options,int cashierId,Action<string,int> authenticate,Action<bool,string> check)
 {
  await using var db=new DentalDbContext(options);var clock=new VietnamClock(SmokeTestTimeProvider.Instance);
  var actorSequence=0;
  async Task<User> Actor(string code)
  {
   var role=await db.Roles.SingleOrDefaultAsync(r=>r.RoleCode==code);if(role is null) { role=new() { RoleCode=code,RoleName=code };db.Roles.Add(role);await db.SaveChangesAsync(); }
   var user=new User { RoleId=role.RoleId,FullName="Synthetic print actor",Phone="098"+(++actorSequence).ToString("D7"),PasswordHash=BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };db.Users.Add(user);await db.SaveChangesAsync();return user;
  }
  var admin=await Actor(RoleCodes.Admin);var doctor=await Actor(RoleCodes.Dentist);var foreign=await Actor(RoleCodes.Dentist);var owner=await Actor(RoleCodes.Patient);var other=await Actor(RoleCodes.Patient);var assistant=await Actor(RoleCodes.Assistant);
  var patient=new Patient { FullName="Synthetic print patient",DateOfBirth=new(1990,1,2),Phone="0990000771",Address="Synthetic patient address",Gender="Female",UserId=owner.UserId };
  var med=new Medicine { Code="PRINT-TEST",Name="Synthetic catalog name",Unit="Unit",UnitPrice=900,IsActive=true,Version=1 };
  var visit=new Visit { Patient=patient,CreatedByUserId=cashierId,DentistId=doctor.UserId,Status=VisitStatuses.Completed,IsLocked=true,Diagnosis="Different visit diagnosis" };
  var invoice=new Invoice { Visit=visit,CreatedByUserId=cashierId,Status=InvoiceStatus.Paid,InvoiceCode="TEST-PRINT-PAID",TotalAmount=1000,PaidAmount=1000,CreatedAt=clock.UtcNow };
  db.Medicines.Add(med);await db.SaveChangesAsync();
  var rx=new Prescription { Visit=visit,Invoice=invoice,DoctorId=doctor.UserId,CreatedByUserId=doctor.UserId,PrescriptionCode="RX-20261017-9001",Diagnosis="Actual saved prescription diagnosis",DoctorAdvice="Saved clinician advice",RevisitDate=new(2026,11,1),CreatedAt=new DateTime(2026,10,16,18,30,0,DateTimeKind.Utc),ReceiverName="Synthetic receiver",ReceiverPhone="0990000772",
   Items=[new() { MedicineId=med.Id,MedicineCode="SNAP",MedicineName="Saved snapshot medicine",Strength="Synthetic strength",Unit="Saved unit",UnitPrice=500,Quantity=2,UsageInstructions="Saved clinician usage",TreatmentDays=3,AllergyOverrideReason="Private clinical override" }] };
  db.Prescriptions.Add(rx);await db.SaveChangesAsync();db.ChangeTracker.Clear();
  var path=$"/api/visits/{visit.VisitId}/prescription/{rx.Id}/print";var body=new PrescriptionPrintRequest(1);const string settingsPath="/api/clinic/print-settings";
  using(var r=await client.PostAsJsonAsync(path,body))check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous print preparation401");
  using(var r=await client.GetAsync(settingsPath))check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous print settings401");
  foreach(var actor in new[] { (RoleCodes.Dentist,doctor.UserId),(RoleCodes.Receptionist,cashierId),(RoleCodes.Assistant,assistant.UserId),(RoleCodes.Patient,owner.UserId) })
  { authenticate(actor.Item1,actor.Item2);using(var r=await client.GetAsync(settingsPath))check(r.StatusCode==HttpStatusCode.Forbidden,"Non-Admin settings read403");using(var r=await client.PutAsJsonAsync(settingsPath,new ClinicPrintSettingsRequest("Synthetic clinic address","0990000773",new string('A',64))))check(r.StatusCode==HttpStatusCode.Forbidden,"Non-Admin settings write403"); }
  authenticate(RoleCodes.Admin,admin.UserId);
  using(var r=await client.PostAsJsonAsync(path,body))check(r.StatusCode==HttpStatusCode.Conflict,"Incomplete print metadata reports setup409 without fabricated values");
  check(!await db.AuditLogs.AnyAsync(a=>a.Action=="MOD_RX_PRINT_PREPARED"),"Failed setup creates no successful preparation audit");
  ClinicPrintSettings settings;
  using(var r=await client.GetAsync(settingsPath)) { settings=(await r.Content.ReadFromJsonAsync<ClinicPrintSettings>())!;check(r.StatusCode==HttpStatusCode.OK && settings.Address is null && settings.Phone is null && settings.Revision.Length==64 && r.Headers.CacheControl?.NoStore==true,"Missing clinic settings honest null/revision NoStore"); }
  using(var r=await client.PutAsJsonAsync(settingsPath,new ClinicPrintSettingsRequest(" ","short",settings.Revision)))check(r.StatusCode==HttpStatusCode.BadRequest,"Invalid clinic settings400");
  var initialRevision=settings.Revision;
  using(var r=await client.PutAsJsonAsync(settingsPath,new ClinicPrintSettingsRequest("Synthetic clinic address","0990000773",initialRevision))) { settings=(await r.Content.ReadFromJsonAsync<ClinicPrintSettings>())!;check(r.StatusCode==HttpStatusCode.OK && settings.Address=="Synthetic clinic address" && settings.Revision!=initialRevision,"Admin saves actual clinic header and new revision"); }
  using(var r=await client.PutAsJsonAsync(settingsPath,new ClinicPrintSettingsRequest("Stale overwrite","0990000774",initialRevision)))check(r.StatusCode==HttpStatusCode.Conflict,"Old clinic settings revision409");
  using(var r=await client.PutAsJsonAsync($"/api/staff/{doctor.UserId}",new UpdateStaffRequest(doctor.FullName,doctor.Phone,null,null,null,RoleCodes.Dentist,"Synthetic approved title")))
  { var staff=await r.Content.ReadFromJsonAsync<StaffAccountResponse>();check(r.StatusCode==HttpStatusCode.OK && staff!.AcademicTitle=="Synthetic approved title","Admin updates academic title through existing staff UI API"); }
  using(var r=await client.PutAsJsonAsync($"/api/staff/{doctor.UserId}",new UpdateStaffRequest(doctor.FullName,doctor.Phone,null,null,null,RoleCodes.Dentist,new string('x',101))))check(r.StatusCode==HttpStatusCode.BadRequest,"Academic title above100 rejected400");
  authenticate(RoleCodes.Dentist,doctor.UserId);
  using(var r=await client.PutAsJsonAsync($"/api/staff/{doctor.UserId}",new UpdateStaffRequest(doctor.FullName,doctor.Phone,null,null,null,RoleCodes.Dentist,"Unapproved self title")))check(r.StatusCode==HttpStatusCode.Forbidden,"Dentist cannot self-change academic title via staff403");
  var png1=SignaturePngFixture.Png();
  using(var r=await client.PostAsJsonAsync($"/api/doctors/{doctor.UserId}/signature",new DoctorSignatureRequest(0,Convert.ToBase64String(png1))))check(r.StatusCode==HttpStatusCode.OK,"Doctor provides real validated PNG before printing");
  foreach(var actor in new[] { (RoleCodes.Admin,admin.UserId),(RoleCodes.Dentist,doctor.UserId),(RoleCodes.Receptionist,cashierId),(RoleCodes.Assistant,assistant.UserId),(RoleCodes.Patient,owner.UserId) })
  {
   authenticate(actor.Item1,actor.Item2);using var r=await client.PostAsJsonAsync(path,body);var d=await r.Content.ReadFromJsonAsync<PrescriptionPrintDocument>();var text=await r.Content.ReadAsStringAsync();
   check(r.StatusCode==HttpStatusCode.OK && r.Headers.CacheControl?.NoStore==true && d!.SignatureVersion==1 && d.SignaturePngBase64==Convert.ToBase64String(png1),"Read-authorized actor prepares scoped signed document NoStore");
   check(d!.PatientName==patient.FullName && d.ClinicAddress==settings.Address && d.ClinicPhone==settings.Phone && d.DoctorTitle=="Synthetic approved title" && d.PrescribedDate==new DateOnly(2026,10,17) && d.Diagnosis==rx.Diagnosis && d.Items.Single().MedicineName=="Saved snapshot medicine" && d.Items.Single().Quantity==2 && d.Items.Single().UsageInstructions=="Saved clinician usage" && d.ReceiverName==rx.ReceiverName && d.ReceiverPhone==rx.ReceiverPhone,"Print uses saved prescription snapshots VN date and actual header/receiver");
   check(!text.Contains("Private clinical override") && !text.Contains("unitPrice",StringComparison.OrdinalIgnoreCase) && !text.Contains("citizenId",StringComparison.OrdinalIgnoreCase) && !text.Contains("password",StringComparison.OrdinalIgnoreCase) && d.RelativeLookupPath=="/portal/prescriptions/"+rx.PrescriptionCode,"Print DTO excludes allergy reason prices identity secrets; QR only relative saved code");
  }
  authenticate(RoleCodes.Dentist,foreign.UserId);using(var r=await client.PostAsJsonAsync(path,body))check(r.StatusCode==HttpStatusCode.NotFound,"Foreign dentist print404");
  authenticate(RoleCodes.Patient,other.UserId);using(var r=await client.PostAsJsonAsync(path,body))check(r.StatusCode==HttpStatusCode.NotFound,"Foreign patient print404");
  authenticate(RoleCodes.Admin,admin.UserId);
  using(var r=await client.PostAsJsonAsync(path,new PrescriptionPrintRequest(0)))check(r.StatusCode==HttpStatusCode.BadRequest,"Invalid print version400");
  using(var r=await client.PostAsJsonAsync(path,new PrescriptionPrintRequest(2)))check(r.StatusCode==HttpStatusCode.Conflict,"Changed prescription version blocks stale document409");
  using(var r=await client.PostAsJsonAsync($"/api/visits/{visit.VisitId}/prescription/{rx.Id+99999}/print",body))check(r.StatusCode==HttpStatusCode.NotFound,"Wrong prescription target404");
  var png2=SignaturePngFixture.Png(width:3);
  using(var r=await client.PostAsJsonAsync($"/api/doctors/{doctor.UserId}/signature",new DoctorSignatureRequest(1,Convert.ToBase64String(png2))))r.EnsureSuccessStatusCode();
  using(var r=await client.PostAsJsonAsync(path,body)) { var d=await r.Content.ReadFromJsonAsync<PrescriptionPrintDocument>();check(r.StatusCode==HttpStatusCode.OK && d!.SignatureVersion==2 && d.SignaturePngBase64==Convert.ToBase64String(png2),"Q318a reprint selects latest signature even on old Paid order"); }
  check(await db.Prescriptions.AnyAsync(p=>p.Id==rx.Id && p.Version==1 && !p.IsCancelled && p.DispensedAt==null) && await db.Invoices.AnyAsync(i=>i.Id==invoice.Id && i.PaidAmount==1000 && i.Status==InvoiceStatus.Paid),"Preparation leaves paid medical and financial contents unchanged");
  var audit=await db.AuditLogs.Where(a=>a.Action=="MOD_RX_PRINT_PREPARED").ToArrayAsync();check(audit.Length==6 && audit.Any(a=>a.Detail!.Contains("\"signatureVersion\":2")) && audit.All(a=>!a.Detail!.Contains(rx.Diagnosis) && !a.Detail.Contains(Convert.ToBase64String(png2))),"Preparation audit records actor/versions without clinical or image content");
  var fault=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailPrintAudit()).Options;var failed=false;
  await using(var ctx=new DentalDbContext(fault))try { await new PrescriptionRepository(ctx,new InvoiceRepository(ctx),clock).PreparePrintAsync(visit.VisitId,rx.Id,admin.UserId,RoleCodes.Admin,body,null,default); }catch(InvalidOperationException e)when(e.Message=="Synthetic print audit fault") { failed=true; }
  check(failed && await db.AuditLogs.CountAsync(a=>a.Action=="MOD_RX_PRINT_PREPARED")==6,"Required print audit fault returns no prepared document");
  var old=new Prescription { VisitId=visit.VisitId,InvoiceId=invoice.Id,DoctorId=doctor.UserId,CreatedByUserId=doctor.UserId,PrescriptionCode="RX-20261017-9000",Diagnosis="Canceled historical",IsCancelled=true,Version=2,CreatedAt=clock.UtcNow };
  db.Prescriptions.Add(old);await db.SaveChangesAsync();db.ChangeTracker.Clear();using(var r=await client.PostAsJsonAsync($"/api/visits/{visit.VisitId}/prescription/{old.Id}/print",new PrescriptionPrintRequest(2)))check(r.StatusCode==HttpStatusCode.Conflict,"Canceled historical prescription cannot print409");
  var otherVisit=new Visit { PatientId=patient.PatientId,CreatedByUserId=cashierId,DentistId=doctor.UserId,Status=VisitStatuses.InProgress };db.Visits.Add(otherVisit);await db.SaveChangesAsync();db.ChangeTracker.Clear();
  using(var r=await client.PostAsJsonAsync($"/api/visits/{otherVisit.VisitId}/prescription/{rx.Id}/print",body))check(r.StatusCode==HttpStatusCode.NotFound,"Prescription cannot be printed under another Visit404");
  async Task<bool> Race(string address) { await using var ctx=new DentalDbContext(options);return(await new ClinicPrintSettingsRepository(ctx).SaveAsync(admin.UserId,new(address,"0990000773",settings.Revision),null,default)).IsSuccess; }
  check((await Task.WhenAll(Race("Synthetic header A"),Race("Synthetic header B"))).Count(ok=>ok)==1,"Concurrent clinic header replacements one winner");
  var current=await new ClinicPrintSettingsRepository(db).ReadAsync(admin.UserId,default);var faultSettings=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailSettingsAudit()).Options;failed=false;
  await using(var ctx=new DentalDbContext(faultSettings))try { await new ClinicPrintSettingsRepository(ctx).SaveAsync(admin.UserId,new("Must roll back","0990000779",current.Value!.Revision),null,default); }catch(InvalidOperationException e)when(e.Message=="Synthetic settings audit fault") { failed=true; }
  check(failed && !await db.SystemConfigs.AnyAsync(s=>s.Value=="Must roll back"),"Clinic settings audit fault rolls back both header fields");
  var transientOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailSettingsTransient()).Options;
  await using(var ctx=new DentalDbContext(transientOptions)) { var result=await new ClinicPrintSettingsRepository(ctx).SaveAsync(admin.UserId,new("Must conflict","0990000779",current.Value!.Revision),null,default);check(result.IsFailure && result.Error.Code=="GEN_004" && !await db.SystemConfigs.AnyAsync(s=>s.Value=="Must conflict"),"Wrapped PostgreSQL transient conflict returns409 result and rolls back"); }
  db.ChangeTracker.Clear();var doctorRow=await db.Users.FindAsync(doctor.UserId);doctorRow!.IsActive=false;await db.SaveChangesAsync();db.ChangeTracker.Clear();
  authenticate(RoleCodes.Dentist,doctor.UserId);using(var r=await client.PostAsJsonAsync(path,body))check(r.StatusCode==HttpStatusCode.NotFound,"Disabled actor cannot prepare print with old JWT404");
  authenticate(RoleCodes.Admin,admin.UserId);using(var r=await client.PostAsJsonAsync(path,body))check(r.StatusCode==HttpStatusCode.OK,"Authorized archive print remains available when original doctor account disabled");
  var adminRow=await db.Users.FindAsync(admin.UserId);adminRow!.RoleId=assistant.RoleId;await db.SaveChangesAsync();db.ChangeTracker.Clear();
  using(var r=await client.GetAsync(settingsPath))check(r.StatusCode==HttpStatusCode.Forbidden,"Changed Admin role cannot use old token for settings403");
  using(var r=await client.PostAsJsonAsync(path,body))check(r.StatusCode==HttpStatusCode.NotFound,"Changed actor role cannot use old Admin print token404");
 }
 private sealed class FailPrintAudit : SaveChangesInterceptor
 {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
  { if(data.Context!.ChangeTracker.Entries<AuditLog>().Any(a=>a.Entity.Action=="MOD_RX_PRINT_PREPARED"))throw new InvalidOperationException("Synthetic print audit fault");return ValueTask.FromResult(result); }
 }
 private sealed class FailSettingsAudit : SaveChangesInterceptor
 {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
  { if(data.Context!.ChangeTracker.Entries<AuditLog>().Any(a=>a.Entity.Action=="MOD_MST_CLINIC_PRINT_SETTINGS_UPDATED"))throw new InvalidOperationException("Synthetic settings audit fault");return ValueTask.FromResult(result); }
 }
 private sealed class FailSettingsTransient : SaveChangesInterceptor
 {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
  { if(data.Context!.ChangeTracker.Entries<AuditLog>().Any(a=>a.Entity.Action=="MOD_MST_CLINIC_PRINT_SETTINGS_UPDATED"))throw new InvalidOperationException("Synthetic wrapped transient",new PostgresException("Synthetic serialization","ERROR","ERROR","40001"));return ValueTask.FromResult(result); }
 }
}
