using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
internal static class PatientPrescriptionHistoryHttpSmoke
{
 public static async Task RunAsync(HttpClient client,DbContextOptions<DentalDbContext> options,int cashierId,Action<string,int> authenticate,Action<bool,string> check)
 {
  await using var db=new DentalDbContext(options);var clock=new VietnamClock(SmokeTestTimeProvider.Instance);var sequence=0;
  async Task<User> Actor(string code)
  {
   var role=await db.Roles.SingleOrDefaultAsync(r=>r.RoleCode==code);if(role is null){role=new(){RoleCode=code,RoleName=code};db.Roles.Add(role);await db.SaveChangesAsync();}
   var u=new User{RoleId=role.RoleId,FullName="Synthetic archive actor "+(++sequence),Phone="098"+sequence.ToString("D7"),PasswordHash=BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),AcademicTitle="Synthetic qualification"};db.Users.Add(u);await db.SaveChangesAsync();return u;
  }
  var admin=await Actor(RoleCodes.Admin);var doctorA=await Actor(RoleCodes.Dentist);var doctorB=await Actor(RoleCodes.Dentist);var doctorC=await Actor(RoleCodes.Dentist);var owner=await Actor(RoleCodes.Patient);var foreignOwner=await Actor(RoleCodes.Patient);var assistant=await Actor(RoleCodes.Assistant);
  var patient=new Patient{FullName="Synthetic archive patient",Phone="0990000771",DateOfBirth=new(1990,1,2),UserId=owner.UserId};var foreign=new Patient{FullName="Synthetic other patient",Phone="0990000772",DateOfBirth=new(1990,1,2),UserId=foreignOwner.UserId};db.Patients.AddRange(patient,foreign);
  var med=new Medicine{Code="ARCHIVE-TEST",Name="Current catalog changed",Unit="unit",UnitPrice=100};db.Medicines.Add(med);await db.SaveChangesAsync();
  async Task<Prescription> Order(User doctor,Patient person,bool cancelled=false,string visitStatus=VisitStatuses.Completed)
  {
   var visit=new Visit{PatientId=person.PatientId,DentistId=doctor.UserId,CreatedByUserId=cashierId,Status=visitStatus,IsLocked=true,Diagnosis="Visit diagnosis only"};
   var invoice=new Invoice{Visit=visit,InvoiceCode="ARCHIVE-"+(++sequence),CreatedByUserId=cashierId,Status=InvoiceStatus.Paid,TotalAmount=200,PaidAmount=200,CreatedAt=clock.UtcNow};
   var rx=new Prescription{Visit=visit,Invoice=invoice,DoctorId=doctor.UserId,CreatedByUserId=doctor.UserId,PrescriptionCode="RX-20261018-"+(9000+sequence),Diagnosis="Saved prescription diagnosis",DoctorAdvice="Saved advice",CreatedAt=clock.UtcNow.AddDays(-2),IsCancelled=cancelled,
    Items=[new(){MedicineId=med.Id,MedicineCode="OLD",MedicineName="Historical snapshot medicine",Unit="saved unit",Quantity=2,UnitPrice=100,UsageInstructions="Saved clinical instruction",AllergyOverrideReason="Private override text",TreatmentDays=2}]};
   db.Prescriptions.Add(rx);await db.SaveChangesAsync();db.ChangeTracker.Clear();return rx;
  }
  var own=await Order(doctorA,patient);var historical=await Order(doctorB,patient);var cancelled=await Order(doctorB,patient,true);var cancelledVisit=await Order(doctorB,patient,visitStatus:VisitStatuses.Cancelled);
  for(var i=0;i<8;i++)await Order(doctorB,patient);
  var other=await Order(doctorB,foreign);
  db.DoctorSignatures.Add(new(){DoctorId=doctorB.UserId,Version=1,ImagePng=SignaturePngFixture.Png(),CreatedAt=clock.UtcNow,UpdatedByUserId=doctorB.UserId});
  db.SystemConfigs.AddRange(new SystemConfig{Key="Clinic:Address",Value="Synthetic clinic address"},new SystemConfig{Key="Clinic:Phone",Value="0990000773"});await db.SaveChangesAsync();db.ChangeTracker.Clear();
  string List(int page=1,int size=10)=>$"/api/patients/{patient.PatientId}/prescriptions?page={page}&pageSize={size}";
  string Detail(Prescription rx,int? patientId=null)=>$"/api/patients/{patientId??patient.PatientId}/prescriptions/{rx.Id}";
  string Print(Prescription rx)=>$"/api/visits/{rx.VisitId}/prescription/{rx.Id}/print";
  async Task<PagedResult<PatientPrescriptionSummary>> Read(int page=1,int size=10){using var r=await client.GetAsync(List(page,size));check(r.StatusCode==HttpStatusCode.OK && r.Headers.CacheControl?.NoStore==true,"History list200/NoStore");return(await r.Content.ReadFromJsonAsync<PagedResult<PatientPrescriptionSummary>>())!;}
  using(var r=await client.GetAsync(List()))check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous history401");
  using(var r=await client.GetAsync(Detail(historical)))check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous detail401");
  authenticate(RoleCodes.Patient,foreignOwner.UserId);using(var r=await client.GetAsync(List()))check(r.StatusCode==HttpStatusCode.NotFound,"Foreign patient cannot list404");using(var r=await client.GetAsync(Detail(historical)))check(r.StatusCode==HttpStatusCode.NotFound,"Foreign patient cannot detail404");
  authenticate(RoleCodes.Dentist,doctorA.UserId);var page=await Read();check(page.TotalCount==1 && page.Items.Single().Id==own.Id,"No current visit dentist only own prescription including count");
  using(var r=await client.GetAsync(Detail(historical)))check(r.StatusCode==HttpStatusCode.NotFound,"No current visit foreign detail404");
  using(var r=await client.PostAsJsonAsync(Print(historical),new PrescriptionPrintRequest(1)))check(r.StatusCode==HttpStatusCode.NotFound,"No current visit foreign print404");
  var care=new Visit{PatientId=patient.PatientId,DentistId=doctorA.UserId,CreatedByUserId=cashierId,Status=VisitStatuses.InProgress};db.Visits.Add(care);await db.SaveChangesAsync();db.ChangeTracker.Clear();
  page=await Read();check(page.TotalCount==12 && page.Items.Count==10 && page.HasNextPage && page.Items.All(x=>x.PatientId==patient.PatientId),"Current care includes entire same-patient archive paged10");
  var second=await Read(2);check(second.Items.Count==2 && second.HasPreviousPage && !second.HasNextPage && !second.Items.Select(x=>x.Id).Intersect(page.Items.Select(x=>x.Id)).Any(),"Stable pages no overlap");
  check(page.Items.Concat(second.Items).Select(x=>x.Id).SequenceEqual(page.Items.Concat(second.Items).OrderByDescending(x=>x.Id).Select(x=>x.Id)),"Equal prescribing timestamps tie-break descendingId");
  check(page.Items.Concat(second.Items).Single(x=>x.Id==historical.Id).VisitDiagnosis=="Visit diagnosis only","Summary uses Visit diagnosis per DL116");
  using(var r=await client.GetAsync(Detail(historical))){var detail=(await r.Content.ReadFromJsonAsync<PatientPrescriptionDetail>())!;var text=await r.Content.ReadAsStringAsync();check(r.StatusCode==HttpStatusCode.OK && r.Headers.CacheControl?.NoStore==true && detail.Diagnosis=="Saved prescription diagnosis" && detail.Items.Single().MedicineName=="Historical snapshot medicine","Read-only detail uses saved medical contents/NoStore");check(!text.Contains("Private override text") && !text.Contains("unitPrice") && !text.Contains("CitizenId") && !text.Contains("PasswordHash"),"History medical DTO omits internal overrides financial prices and identity secrets");}
  using(var r=await client.GetAsync(Detail(other)))check(r.StatusCode==HttpStatusCode.NotFound,"Cannot mix prescription from different patient404");
  using(var r=await client.GetAsync($"/api/patients/{foreign.PatientId}/prescriptions/{other.Id}"))check(r.StatusCode==HttpStatusCode.NotFound,"Current care of one patient does not unlock another404");
  using(var r=await client.GetAsync(Detail(cancelled))){var detail=(await r.Content.ReadFromJsonAsync<PatientPrescriptionDetail>())!;check(r.StatusCode==HttpStatusCode.OK && detail.IsCancelled && !detail.CanPrint,"Canceled history visible but cannot print");}
  using(var r=await client.PostAsJsonAsync(Print(cancelled),new PrescriptionPrintRequest(1)))check(r.StatusCode==HttpStatusCode.Conflict,"Canceled prescription print409");
  using(var r=await client.GetAsync(Detail(cancelledVisit))){var detail=(await r.Content.ReadFromJsonAsync<PatientPrescriptionDetail>())!;check(!detail.CanPrint,"Canceled visit prohibits reprint even uncanceled Rx");}
  using(var r=await client.PostAsJsonAsync(Print(cancelledVisit),new PrescriptionPrintRequest(1)))check(r.StatusCode==HttpStatusCode.Conflict,"Canceled visit print409");
  using(var r=await client.PostAsJsonAsync(Print(historical),new PrescriptionPrintRequest(1)))check(r.StatusCode==HttpStatusCode.OK,"Current care dentist reprints colleague prescription200");
  check(await db.AuditLogs.AnyAsync(a=>a.UserId==doctorA.UserId && a.EntityId==historical.Id && a.Action=="MOD_RX_PRINT_PREPARED" && a.Detail!.Contains("\"sharedHistoryAccess\":true")),"Cross-doctor preparation audit records actual actor and scope");
  db.DoctorSignatures.Add(new(){DoctorId=doctorB.UserId,Version=2,ImagePng=SignaturePngFixture.Png(width:3),CreatedAt=clock.UtcNow,UpdatedByUserId=doctorB.UserId});await db.SaveChangesAsync();db.ChangeTracker.Clear();
  using(var r=await client.PostAsJsonAsync(Print(historical),new PrescriptionPrintRequest(1))){var print=(await r.Content.ReadFromJsonAsync<PrescriptionPrintDocument>())!;check(r.StatusCode==HttpStatusCode.OK && print.SignatureVersion==2 && print.DoctorName==doctorB.FullName && print.Diagnosis==historical.Diagnosis,"Historical reprint uses original prescribing doctor's current signature and saved Rx");}
  using(var r=await client.PostAsJsonAsync(Print(historical),new PrescriptionPrintRequest(2)))check(r.StatusCode==HttpStatusCode.Conflict,"Shared print still requires correct version409");
  using(var r=await client.GetAsync($"/api/visits/{historical.VisitId}/prescription"))check(r.StatusCode==HttpStatusCode.NotFound,"General clinical context remains original owner scope404");
  using(var r=await client.GetAsync($"/api/doctors/{doctorB.UserId}/signature"))check(r.StatusCode==HttpStatusCode.NotFound,"Read history does not grant colleague signature management404");
  var preview=new PrescriptionWriteRequest(1,"Must not edit",[new(med.Id,med.Version,1,"Synthetic usage")]);
  using(var r=await client.PostAsJsonAsync($"/api/visits/{historical.VisitId}/prescription/preview",preview))check(r.StatusCode==HttpStatusCode.NotFound,"Current care cannot preview change colleague's old prescription404");
  using(var r=await client.PostAsJsonAsync($"/api/visits/{historical.VisitId}/prescription/{historical.Id}/cancel",new PrescriptionCancellationRequest(1)))check(r.StatusCode==HttpStatusCode.NotFound,"Current care cannot cancel colleague's old prescription404");
  using(var r=await client.PostAsJsonAsync($"/api/prescriptions/{historical.Id}/dispense",new PrescriptionDispenseRequest(1,true)))check(r.StatusCode==HttpStatusCode.Forbidden,"Current care does not grant handover403");
  async Task CareStatus(string status){db.ChangeTracker.Clear();var row=await db.Visits.FindAsync(care.VisitId);row!.Status=status;await db.SaveChangesAsync();db.ChangeTracker.Clear();}
  await CareStatus(VisitStatuses.Completed);page=await Read();check(page.TotalCount==1,"Care completion immediately narrows list back to own");
  using(var r=await client.GetAsync(Detail(historical)))check(r.StatusCode==HttpStatusCode.NotFound,"Care completion revokes historical detail404");
  using(var r=await client.PostAsJsonAsync(Print(historical),new PrescriptionPrintRequest(1)))check(r.StatusCode==HttpStatusCode.NotFound,"Care completion revokes new print preparation404");
  await CareStatus(VisitStatuses.Correcting);using(var r=await client.GetAsync(Detail(historical)))check(r.StatusCode==HttpStatusCode.OK,"Correcting care grants read200");using(var r=await client.PostAsJsonAsync(Print(historical),new PrescriptionPrintRequest(1)))check(r.StatusCode==HttpStatusCode.OK,"Correcting care grants reprint200");
  await CareStatus(VisitStatuses.Created);page=await Read();check(page.TotalCount==1,"Created visit not sufficient for shared archive");
  await CareStatus(VisitStatuses.Cancelled);page=await Read();check(page.TotalCount==1,"Canceled care not sufficient for shared archive");
  var wrongCare=new Visit{PatientId=foreign.PatientId,DentistId=doctorC.UserId,CreatedByUserId=cashierId,Status=VisitStatuses.InProgress};db.Visits.Add(wrongCare);await db.SaveChangesAsync();db.ChangeTracker.Clear();authenticate(RoleCodes.Dentist,doctorC.UserId);page=await Read();check(page.TotalCount==0,"Different-patient active care yields no foreign rows/count");
  foreach(var actor in new[]{(RoleCodes.Admin,admin.UserId),(RoleCodes.Assistant,assistant.UserId),(RoleCodes.Receptionist,cashierId),(RoleCodes.Patient,owner.UserId)}){authenticate(actor.Item1,actor.Item2);page=await Read();check(page.TotalCount==12,"Existing authorized support or patient reader sees own permitted patient archive");using var response=await client.GetAsync(Detail(historical));check(response.StatusCode==HttpStatusCode.OK,"Authorized reader may open historical medical detail");}
  authenticate(RoleCodes.Admin,admin.UserId);foreach(var query in new[]{List(0),List(1,51),List(1000001)}){using var response=await client.GetAsync(query);check(response.StatusCode==HttpStatusCode.BadRequest,"Invalid paging400");}
  using(var r=await client.GetAsync($"/api/patients/2147483647/prescriptions"))check(r.StatusCode==HttpStatusCode.NotFound,"Missing patient404");
  check(await db.Prescriptions.AnyAsync(p=>p.Id==historical.Id && p.Version==1 && p.Diagnosis=="Saved prescription diagnosis") && await db.Invoices.AnyAsync(i=>i.Id==historical.InvoiceId && i.Status==InvoiceStatus.Paid && i.TotalAmount==200 && i.PaidAmount==200),"All archive read/print leave clinical and financial data unchanged");
  await CareStatus(VisitStatuses.InProgress);var faultOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailPrintAudit()).Options;var auditCount=await db.AuditLogs.CountAsync();var failed=false;
  await using(var ctx=new DentalDbContext(faultOptions))try{await new PrescriptionRepository(ctx,new InvoiceRepository(ctx),clock).PreparePrintAsync(historical.VisitId,historical.Id,doctorA.UserId,RoleCodes.Dentist,new(1),null,default);}catch(InvalidOperationException e)when(e.Message=="Synthetic shared print audit failure"){failed=true;}
  check(failed && await db.AuditLogs.CountAsync()==auditCount,"Shared print requires audit and rolls back on audit failure");
  var disabled=await db.Users.FindAsync(doctorA.UserId);disabled!.IsActive=false;await db.SaveChangesAsync();db.ChangeTracker.Clear();authenticate(RoleCodes.Dentist,doctorA.UserId);
  using(var r=await client.GetAsync(List()))check(r.StatusCode==HttpStatusCode.NotFound,"Disabled actor stale JWT list404");using(var r=await client.GetAsync(Detail(historical)))check(r.StatusCode==HttpStatusCode.NotFound,"Disabled actor stale JWT detail404");using(var r=await client.PostAsJsonAsync(Print(historical),new PrescriptionPrintRequest(1)))check(r.StatusCode==HttpStatusCode.NotFound,"Disabled actor stale JWT print404");
 }
 private sealed class FailPrintAudit:SaveChangesInterceptor
 {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
  {if(data.Context!.ChangeTracker.Entries<AuditLog>().Any(e=>e.State==EntityState.Added && e.Entity.Action=="MOD_RX_PRINT_PREPARED"))throw new InvalidOperationException("Synthetic shared print audit failure");return base.SavingChangesAsync(data,result,ct);}
 }
}
