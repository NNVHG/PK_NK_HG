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
internal static class PrescriptionHandoverHttpSmoke
{
 public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int cashierId, Action<string,int> authenticate, Action<bool,string> check)
 {
  await using var db=new DentalDbContext(options);var clock=new VietnamClock(SmokeTestTimeProvider.Instance);var sequence=0;
  async Task<User> Actor(string code)
  {
   var role=await db.Roles.SingleOrDefaultAsync(r=>r.RoleCode==code);if(role is null){role=new(){RoleCode=code,RoleName=code};db.Roles.Add(role);await db.SaveChangesAsync();}
   var user=new User{RoleId=role.RoleId,FullName="Synthetic handover actor "+(++sequence),Phone="098"+sequence.ToString("D7"),PasswordHash=BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString())};db.Users.Add(user);await db.SaveChangesAsync();return user;
  }
  var admin=await Actor(RoleCodes.Admin);var assistant=await Actor(RoleCodes.Assistant);var doctor=await Actor(RoleCodes.Dentist);var foreign=await Actor(RoleCodes.Dentist);var owner=await Actor(RoleCodes.Patient);var other=await Actor(RoleCodes.Patient);
  var patient=new Patient{FullName="Synthetic adult",DateOfBirth=clock.Today.AddYears(-30),Phone="0990000111",UserId=owner.UserId};db.Patients.Add(patient);
  var child=new Patient{FullName="Synthetic child",DateOfBirth=clock.Today.AddYears(-10),Phone="0990000112",UserId=owner.UserId};db.Patients.Add(child);
  var medicine=new Medicine{Code="HANDOVER-TEST",Name="Synthetic medicine",Unit="unit",UnitPrice=500};db.Medicines.Add(medicine);await db.SaveChangesAsync();
  db.MedicineStockBalances.Add(new(){MedicineId=medicine.Id,OnHand=1000,UpdatedAt=clock.UtcNow});await db.SaveChangesAsync();
  async Task<Prescription> Order(Patient? person=null,InvoiceStatus invoiceStatus=InvoiceStatus.Paid,double ageDays=0,bool cancelled=false,DateTime? handedAt=null,decimal discount=0,decimal? actualPaid=null)
  {
   var visit=new Visit{PatientId=(person??patient).PatientId,DentistId=doctor.UserId,CreatedByUserId=cashierId,Status=VisitStatuses.Completed,IsLocked=true};
   var invoice=new Invoice{Visit=visit,CreatedByUserId=cashierId,InvoiceCode="HANDOVER-"+(++sequence),Status=invoiceStatus,TotalAmount=1000,DiscountAmount=discount,DiscountType=discount>0?"FixedAmount":null,DiscountValue=discount>0?discount:null,PaidAmount=actualPaid??(invoiceStatus==InvoiceStatus.Paid?1000-discount:0),CreatedAt=clock.UtcNow};
   var rx=new Prescription{Visit=visit,Invoice=invoice,DoctorId=doctor.UserId,CreatedByUserId=doctor.UserId,PrescriptionCode="RX-20261018-"+(9000+sequence),Diagnosis="Saved clinical content",CreatedAt=clock.UtcNow.AddDays(-ageDays),IsCancelled=cancelled,DispensedAt=handedAt,DispensedBy=handedAt is null?null:cashierId,
    Items=[new(){MedicineId=medicine.Id,MedicineCode=medicine.Code,MedicineName=medicine.Name,Unit="unit",UnitPrice=500,Quantity=2,UsageInstructions="Saved clinician instructions"}]};
   db.Prescriptions.Add(rx);await db.SaveChangesAsync();db.ChangeTracker.Clear();return rx;
  }
  static string Path(Prescription rx,string action="dispense")=>$"/api/prescriptions/{rx.Id}/{action}";
  async Task<PrescriptionHandoverStatus> Read(Prescription rx){using var r=await client.GetAsync(Path(rx));check(r.StatusCode==HttpStatusCode.OK && r.Headers.CacheControl?.NoStore==true,"Read scoped status NoStore200");return(await r.Content.ReadFromJsonAsync<PrescriptionHandoverStatus>())!;}
  var rx=await Order();using(var response=await client.GetAsync(Path(rx)))check(response.StatusCode==HttpStatusCode.Unauthorized,"Anonymous read401");
  using(var response=await client.PostAsJsonAsync(Path(rx),new PrescriptionDispenseRequest(1,true)))check(response.StatusCode==HttpStatusCode.Unauthorized,"Anonymous handover401");
  foreach(var actor in new[]{(RoleCodes.Patient,owner.UserId),(RoleCodes.Dentist,doctor.UserId)})
  {authenticate(actor.Item1,actor.Item2);using var response=await client.PostAsJsonAsync(Path(rx),new PrescriptionDispenseRequest(1,true));check(response.StatusCode==HttpStatusCode.Forbidden,"Clinical reader cannot handover403");using var undo=await client.PostAsJsonAsync(Path(rx,"undo-dispense"),new PrescriptionUndoDispenseRequest(1,true,"Synthetic reason"));check(undo.StatusCode==HttpStatusCode.Forbidden,"Clinical reader cannot undo403");}
  foreach(var actor in new[]{(RoleCodes.Patient,other.UserId),(RoleCodes.Dentist,foreign.UserId)}){authenticate(actor.Item1,actor.Item2);using var response=await client.GetAsync(Path(rx));check(response.StatusCode==HttpStatusCode.NotFound,"Foreign medical reader404");}
  authenticate(RoleCodes.Receptionist,cashierId);var state=await Read(rx);check(state.CanDispense && !state.CanUndo && !state.ReceiverRequired,"Paid adult ready");
  using(var response=await client.PostAsJsonAsync(Path(rx),new PrescriptionDispenseRequest(1,false)))check(response.StatusCode==HttpStatusCode.BadRequest,"Explicit confirmation required400");
  using(var response=await client.PostAsJsonAsync(Path(rx),new PrescriptionDispenseRequest(0,true)))check(response.StatusCode==HttpStatusCode.BadRequest,"Invalid version400");
  using(var response=await client.PostAsJsonAsync(Path(rx),new PrescriptionDispenseRequest(2,true)))check(response.StatusCode==HttpStatusCode.Conflict,"Stale version409");
  using(var response=await client.PostAsJsonAsync(Path(rx),new PrescriptionDispenseRequest(1,true,true,"Synthetic receiver","0990000123"))){state=(await response.Content.ReadFromJsonAsync<PrescriptionHandoverStatus>())!;check(response.StatusCode==HttpStatusCode.OK && state.Version==2 && state.DispensedAt==clock.UtcNow && state.CanUndo && state.DispensedByName is not null,"Cashier hands over and receives current actor/time/version");}
  check(await db.PrescriptionDispenseEvents.CountAsync(e=>e.PrescriptionId==rx.Id)==1 && await db.AuditLogs.AnyAsync(a=>a.EntityId==rx.Id && a.Action=="PRESCRIPTION_DISPENSED"),"Required operational history and audit persisted");
  check(await db.Prescriptions.AnyAsync(p=>p.Id==rx.Id && p.ReceiverName=="Synthetic receiver" && p.DispenseNote!=null) && await db.Invoices.AnyAsync(i=>i.Id==rx.InvoiceId && i.Status==InvoiceStatus.Paid && i.PaidAmount==1000),"Receiver retained for print; financial record unchanged");
  using(var response=await client.PostAsJsonAsync(Path(rx),new PrescriptionDispenseRequest(1,true)))check(response.StatusCode==HttpStatusCode.Conflict,"Replay stale dispense409");
  using(var response=await client.PostAsJsonAsync(Path(rx),new PrescriptionDispenseRequest(2,true)))check(response.StatusCode==HttpStatusCode.Conflict,"Already dispensed409");
  authenticate(RoleCodes.Assistant,assistant.UserId);state=await Read(rx);check(!state.CanUndo,"Other handover operator cannot undo");
  using(var response=await client.PostAsJsonAsync(Path(rx,"undo-dispense"),new PrescriptionUndoDispenseRequest(2,true,"Synthetic correction")))check(response.StatusCode==HttpStatusCode.Conflict,"Other operator undo409");
  authenticate(RoleCodes.Admin,admin.UserId);
  using(var response=await client.PostAsJsonAsync(Path(rx,"undo-dispense"),new PrescriptionUndoDispenseRequest(2,true,"tiny")))check(response.StatusCode==HttpStatusCode.BadRequest,"Undo reason min5 enforced400");
  using(var response=await client.PostAsJsonAsync(Path(rx,"undo-dispense"),new PrescriptionUndoDispenseRequest(2,true,"Synthetic correction"))){state=(await response.Content.ReadFromJsonAsync<PrescriptionHandoverStatus>())!;check(response.StatusCode==HttpStatusCode.OK && state.Version==3 && state.DispensedAt is null && state.ReceiverName is null,"Admin undoes Paid metadata only");}
  check(await db.PrescriptionDispenseEvents.CountAsync(e=>e.PrescriptionId==rx.Id)==2 && await db.PrescriptionDispenseEvents.AnyAsync(e=>e.PrescriptionId==rx.Id && e.Reason=="Synthetic correction" && e.ReceiverName=="Synthetic receiver"),"Undo preserves previous receiver in append-only history");
  check(await db.PrescriptionItems.AnyAsync(i=>i.PrescriptionId==rx.Id && i.Quantity==2 && i.UsageInstructions=="Saved clinician instructions") && !await db.PaymentTransactions.AnyAsync(),"Undo leaves clinical items and financial transactions untouched");
  authenticate(RoleCodes.Assistant,assistant.UserId);using(var response=await client.PostAsJsonAsync(Path(rx),new PrescriptionDispenseRequest(3,true)))check(response.StatusCode==HttpStatusCode.OK,"Assistant can handover again after legitimate undo");
  using(var response=await client.PostAsJsonAsync(Path(rx,"undo-dispense"),new PrescriptionUndoDispenseRequest(4,true,"Synthetic own correction")))check(response.StatusCode==HttpStatusCode.OK,"Original Assistant may undo own handover while Paid");
  var unpaid=await Order(invoiceStatus:InvoiceStatus.Draft);using(var response=await client.PostAsJsonAsync(Path(unpaid),new PrescriptionDispenseRequest(1,true)))check(response.StatusCode==HttpStatusCode.Conflict,"Unpaid cannot handover409");
  var cancelled=await Order(cancelled:true);using(var response=await client.PostAsJsonAsync(Path(cancelled),new PrescriptionDispenseRequest(1,true)))check(response.StatusCode==HttpStatusCode.Conflict,"Canceled prescription cannot handover409");
  var pediatric=await Order(child);state=await Read(pediatric);check(state.ReceiverRequired,"Under15 requires proxy receiver");
  using(var response=await client.PostAsJsonAsync(Path(pediatric),new PrescriptionDispenseRequest(1,true)))check(response.StatusCode==HttpStatusCode.BadRequest,"Under15 self receiver rejected400");
  using(var response=await client.PostAsJsonAsync(Path(pediatric),new PrescriptionDispenseRequest(1,true,true,"Synthetic guardian")))check(response.StatusCode==HttpStatusCode.BadRequest,"Under15 missing phone rejected400");
  using(var response=await client.PostAsJsonAsync(Path(pediatric),new PrescriptionDispenseRequest(1,true,true,"Synthetic guardian","0990000123")))check(response.StatusCode==HttpStatusCode.OK,"Under15 valid guardian accepted");
  var expired=await Order(ageDays:5.01);using(var response=await client.PostAsJsonAsync(Path(expired),new PrescriptionDispenseRequest(1,true)))check(response.StatusCode==HttpStatusCode.Conflict,"Over five days blocked409");
  using(var response=await client.PostAsJsonAsync(Path(expired),new PrescriptionDispenseRequest(1,true,OverrideExpired:true,OverrideReason:"Synthetic exception")))check(response.StatusCode==HttpStatusCode.Forbidden,"NonAdmin expiry override403");
  authenticate(RoleCodes.Admin,admin.UserId);using(var response=await client.PostAsJsonAsync(Path(expired),new PrescriptionDispenseRequest(1,true,OverrideExpired:true,OverrideReason:"Synthetic exception")))check(response.StatusCode==HttpStatusCode.OK,"Admin expiry exception requires explicit reason");
  var border=await Order(handedAt:clock.UtcNow.AddMinutes(-30));using(var response=await client.PostAsJsonAsync(Path(border,"undo-dispense"),new PrescriptionUndoDispenseRequest(1,true,"Synthetic boundary")))check(response.StatusCode==HttpStatusCode.OK,"Exactly30minutes undo accepted");
  var late=await Order(handedAt:clock.UtcNow.AddMinutes(-30).AddTicks(-10));using(var response=await client.PostAsJsonAsync(Path(late,"undo-dispense"),new PrescriptionUndoDispenseRequest(1,true,"Synthetic late")))check(response.StatusCode==HttpStatusCode.Conflict,"After30minutes Admin also blocked409");
  var race=await Order();async Task<bool> Race(){await using var ctx=new DentalDbContext(options);return(await new PrescriptionHandoverRepository(ctx,clock).DispenseAsync(race.Id,admin.UserId,RoleCodes.Admin,new(1,true),null,default)).IsSuccess;}
  check((await Task.WhenAll(Race(),Race())).Count(x=>x)==1 && await db.PrescriptionDispenseEvents.CountAsync(e=>e.PrescriptionId==race.Id)==1,"Concurrent handovers exactly one winner/event");
  var fiveDays=await Order(ageDays:5);using(var response=await client.PostAsJsonAsync(Path(fiveDays),new PrescriptionDispenseRequest(1,true)))check(response.StatusCode==HttpStatusCode.OK,"Exactly five elapsed days accepted");
  var birthday=new Patient{FullName="Synthetic fifteenth birthday",DateOfBirth=clock.Today.AddYears(-15),Phone="0990000119",UserId=owner.UserId};db.Patients.Add(birthday);await db.SaveChangesAsync();var fifteen=await Order(birthday);state=await Read(fifteen);check(!state.ReceiverRequired,"Exactly15 birthday proxy optional");
  var undoFault=await Order(handedAt:clock.UtcNow);var undoOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;var undoFailed=false;
  await using(var ctx=new DentalDbContext(undoOptions))try{await new PrescriptionHandoverRepository(ctx,clock).UndoAsync(undoFault.Id,admin.UserId,RoleCodes.Admin,new(1,true,"Synthetic rollback reason"),null,default);}catch(InvalidOperationException e)when(e.Message=="Synthetic handover audit failure"){undoFailed=true;}
  check(undoFailed && await db.Prescriptions.AnyAsync(p=>p.Id==undoFault.Id && p.DispensedAt!=null && p.Version==1) && !await db.PrescriptionDispenseEvents.AnyAsync(e=>e.PrescriptionId==undoFault.Id),"Undo audit fault preserves prior handed-over state/version");
  check(await db.Invoices.AnyAsync(i=>i.Id==rx.InvoiceId && i.Status==InvoiceStatus.Paid && i.PaidAmount==1000 && i.TotalAmount==1000),"Repeated handover and undo retain Paid invoice amounts");
  var discounted=await Order(discount:100);state=await Read(discounted);check(state.CanDispense,"Paid net amount after discount unlocks handover");
  using(var response=await client.PostAsJsonAsync(Path(discounted),new PrescriptionDispenseRequest(1,true)))check(response.StatusCode==HttpStatusCode.OK,"Discounted Paid invoice hands over200");
  check(await db.Invoices.AnyAsync(i=>i.Id==discounted.InvoiceId && i.TotalAmount==1000 && i.DiscountAmount==100 && i.PaidAmount==900 && i.Status==InvoiceStatus.Paid),"Handover preserves gross discount and net paid values");
  var grossPaid=await Order(discount:100,actualPaid:1000);state=await Read(grossPaid);check(!state.CanDispense,"Gross amount in Paid field cannot masquerade as settled discounted invoice");
  using(var response=await client.PostAsJsonAsync(Path(grossPaid),new PrescriptionDispenseRequest(1,true)))check(response.StatusCode==HttpStatusCode.Conflict,"Inconsistent overpaid discounted invoice blocked409");
  var shortPaid=await Order(discount:100,actualPaid:800);using(var response=await client.PostAsJsonAsync(Path(shortPaid),new PrescriptionDispenseRequest(1,true)))check(response.StatusCode==HttpStatusCode.Conflict,"Net underpaid invoice blocked despite Paid status409");
  var zeroPayable=await Order(discount:1000);state=await Read(zeroPayable);check(state.CanDispense,"Zero-final invoice settled by approved discount is ready");
  using(var response=await client.PostAsJsonAsync(Path(zeroPayable),new PrescriptionDispenseRequest(1,true)))check(response.StatusCode==HttpStatusCode.OK,"Zero-final Paid invoice can hand over200");
  check(!await db.PaymentTransactions.AnyAsync(t=>t.InvoiceId==zeroPayable.InvoiceId),"Zero-final handover creates no artificial zero receipt");
  var partialDiscounted=await Order(invoiceStatus:InvoiceStatus.PartiallyPaid,discount:100,actualPaid:400);state=await Read(partialDiscounted);check(!state.CanDispense,"Discount does not permit PartiallyPaid handover");
  using(var response=await client.PostAsJsonAsync(Path(partialDiscounted),new PrescriptionDispenseRequest(1,true)))check(response.StatusCode==HttpStatusCode.Conflict,"PartiallyPaid with discount still blocked409");
  var faultRx=await Order();var faultOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;var failed=false;
  await using(var ctx=new DentalDbContext(faultOptions))try{await new PrescriptionHandoverRepository(ctx,clock).DispenseAsync(faultRx.Id,admin.UserId,RoleCodes.Admin,new(1,true),null,default);}catch(InvalidOperationException e)when(e.Message=="Synthetic handover audit failure"){failed=true;}
  check(failed && await db.Prescriptions.AnyAsync(p=>p.Id==faultRx.Id && p.Version==1 && p.DispensedAt==null) && !await db.PrescriptionDispenseEvents.AnyAsync(e=>e.PrescriptionId==faultRx.Id),"Required audit fault rolls back status/version/event");
  db.ChangeTracker.Clear();var protectedRx=await db.Prescriptions.FindAsync(race.Id);protectedRx!.Diagnosis="Must not change";failed=false;try{await db.SaveChangesAsync();}catch(InvalidOperationException){failed=true;}check(failed,"Ordinary save cannot edit handed-over clinical contents");db.ChangeTracker.Clear();
  var ev=await db.PrescriptionDispenseEvents.FirstAsync();ev.Reason="Must not overwrite";failed=false;try{await db.SaveChangesAsync();}catch(InvalidOperationException){failed=true;}check(failed,"Operational history immutable");db.ChangeTracker.Clear();
  var disabled=await db.Users.FindAsync(assistant.UserId);disabled!.IsActive=false;await db.SaveChangesAsync();db.ChangeTracker.Clear();authenticate(RoleCodes.Assistant,assistant.UserId);using(var response=await client.GetAsync(Path(rx)))check(response.StatusCode==HttpStatusCode.NotFound,"Disabled actor stale JWT read404");
  using(var response=await client.PostAsJsonAsync(Path(rx),new PrescriptionDispenseRequest(5,true)))check(response.StatusCode==HttpStatusCode.NotFound,"Disabled actor stale JWT write404");
  check(!await db.AuditLogs.AnyAsync(a=>a.Action.StartsWith("PRESCRIPTION_DISPENSE") && (a.Detail!.Contains("Synthetic receiver") || a.Detail.Contains("0990000123") || a.Detail.Contains("Synthetic correction"))),"Generic audit contains metadata only");
 }
 private sealed class FailAudit:SaveChangesInterceptor
 {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
  {if(data.Context!.ChangeTracker.Entries<AuditLog>().Any(e=>e.State==EntityState.Added && e.Entity.Action.StartsWith("PRESCRIPTION_DISPENSE")))throw new InvalidOperationException("Synthetic handover audit failure");return base.SavingChangesAsync(data,result,ct);}
 }
}
