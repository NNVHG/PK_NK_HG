using System.Data;
using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_PAT;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
internal static class PrescriptionReplacementHttpSmoke
{
 public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int cashierId, Action<string,int> authenticate, Action<bool,string> check)
 {
  await using var db=new DentalDbContext(options); var clock=new VietnamClock(SmokeTestTimeProvider.Instance);
  async Task<User> Actor(string code)
  {
   var role=await db.Roles.SingleOrDefaultAsync(x=>x.RoleCode==code);if(role is null) { role=new() { RoleCode=code,RoleName=code };db.Roles.Add(role);await db.SaveChangesAsync(); }
   var actor=new User { FullName="Synthetic replacement actor",Phone="REPL"+Guid.NewGuid().ToString("N")[..12],RoleId=role.RoleId,PasswordHash=BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };db.Users.Add(actor);await db.SaveChangesAsync();return actor;
  }
  var doctor=await Actor(RoleCodes.Dentist);var foreign=await Actor(RoleCodes.Dentist);var admin=await Actor(RoleCodes.Admin);var assistant=await Actor(RoleCodes.Assistant);var owner=await Actor(RoleCodes.Patient);var outsider=await Actor(RoleCodes.Patient);
  var patient=new Patient { FullName="Synthetic replacement patient",Phone="0990000881",DateOfBirth=new(1990,1,1),UserId=owner.UserId,CitizenIdHash=CitizenIdIdentity.Hash("987654320001"),CitizenIdMasked="987******001",CitizenIdVersion=1 };
  var med=new Medicine { Code="REPL",Name="Synthetic medicine",Unit="Unit",UnitPrice=500,IsActive=true,Version=1 };
  var catalog=new DentalService { Code="REPL-SVC",Name="Synthetic service",DurationMinutes=15,Prices=[new() { Amount=10000,EffectiveFrom=clock.UtcNow,CreatedByUserId=admin.UserId }] };
  db.Patients.Add(patient);db.Medicines.Add(med);db.DentalServices.Add(catalog);await db.SaveChangesAsync();var price=catalog.Prices.Single().ServicePriceId;db.ChangeTracker.Clear();
  async Task<int> Visit()
  {
   var v=new Visit { PatientId=patient.PatientId,DentistId=doctor.UserId,CreatedByUserId=cashierId,Status=VisitStatuses.InProgress,Diagnosis="Synthetic visit diagnosis",StartedAt=clock.UtcNow };
   db.Visits.Add(v);await db.SaveChangesAsync();db.VisitServices.Add(new() { VisitId=v.VisitId,ServiceId=catalog.DentalServiceId,ServicePriceId=price,ServiceCode=catalog.Code,ServiceName=catalog.Name,Quantity=1,UnitPrice=10000,CreatedByUserId=doctor.UserId,CreatedAt=clock.UtcNow });await db.SaveChangesAsync();db.ChangeTracker.Clear();return v.VisitId;
  }
  string Path(int id)=>$"/api/visits/{id}/prescription";
  PrescriptionRepository Repo(DentalDbContext ctx)=>new(ctx,new InvoiceRepository(ctx),clock);
  PrescriptionWriteRequest Request()=>new(0,"Synthetic prescription diagnosis",[new(med.Id,med.Version,2,"Synthetic doctor instruction")],"Synthetic advice");
  async Task<PrescriptionWriteRequest> Preview(int visit)
  { using var r=await client.PostAsJsonAsync(Path(visit)+"/preview",Request());if(!r.IsSuccessStatusCode)throw new Exception("Synthetic replacement preview failed: "+r.StatusCode);return Request() with { PreviewToken=(await r.Content.ReadFromJsonAsync<PrescriptionPreview>())!.Token }; }
  async Task<PrescriptionResponse> Create(int visit,PrescriptionWriteRequest? body=null)
  { using var r=await client.PostAsJsonAsync(Path(visit),body??await Preview(visit));if(r.StatusCode!=HttpStatusCode.Created)throw new Exception("Synthetic replacement create failed: "+await r.Content.ReadAsStringAsync());return(await r.Content.ReadFromJsonAsync<PrescriptionResponse>())!; }
  var id=await Visit();var path=Path(id);var cancelPath=path+"/1/cancel";
  using(var r=await client.PostAsJsonAsync(cancelPath,new PrescriptionCancellationRequest(1)))check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous cancellation401");
  using(var r=await client.GetAsync(path+"/history"))check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous history401");
  foreach(var actor in new[] { (RoleCodes.Admin,admin.UserId),(RoleCodes.Assistant,assistant.UserId),(RoleCodes.Patient,owner.UserId),(RoleCodes.Receptionist,cashierId) })
  { authenticate(actor.Item1,actor.Item2);using var r=await client.PostAsJsonAsync(cancelPath,new PrescriptionCancellationRequest(1));check(r.StatusCode==HttpStatusCode.Forbidden,"Only Dentist cancellation403"); }
  authenticate(RoleCodes.Dentist,doctor.UserId);var oldPreview=await Preview(id);var first=await Create(id,oldPreview);cancelPath=path+$"/{first.Id}/cancel";
  using(var r=await client.GetAsync(path))check((await r.Content.ReadFromJsonAsync<PrescriptionContext>())!.CanCancel,"Owner context advertises cancellation for unpaid Draft");
  using(var r=await client.PostAsJsonAsync($"/api/public/prescriptions/{first.PrescriptionCode}/verify",new { citizenId="987654320001" }))
  { r.EnsureSuccessStatusCode();var body=await r.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();client.DefaultRequestHeaders.Add("X-Prescription-View-Token",body.GetProperty("token").GetString()); }
  var oldItems=await db.PrescriptionItems.AsNoTracking().Where(i=>i.PrescriptionId==first.Id).Select(i=>new { i.Id,i.MedicineName,i.Quantity,i.UsageInstructions,i.UnitPrice }).ToListAsync();
  authenticate(RoleCodes.Dentist,foreign.UserId);using(var r=await client.PostAsJsonAsync(cancelPath,new PrescriptionCancellationRequest(1)))check(r.StatusCode==HttpStatusCode.NotFound,"Foreign dentist cancel404");
  using(var r=await client.GetAsync(path+"/history"))check(r.StatusCode==HttpStatusCode.NotFound,"Foreign dentist history404");
  authenticate(RoleCodes.Dentist,doctor.UserId);
  using(var r=await client.PostAsJsonAsync(cancelPath,new PrescriptionCancellationRequest(0)))check(r.StatusCode==HttpStatusCode.BadRequest,"Cancel version0 rejected400");
  using(var r=await client.PostAsJsonAsync(cancelPath,new PrescriptionCancellationRequest(2)))check(r.StatusCode==HttpStatusCode.Conflict,"Stale cancel version409");
  var faultOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailCancellationAudit()).Options;var failed=false;
  await using(var ctx=new DentalDbContext(faultOptions))try { await Repo(ctx).CancelAsync(id,first.Id,doctor.UserId,new(1),null,default); }catch(InvalidOperationException e)when(e.Message=="Synthetic cancel audit fault") { failed=true; }
  check(failed && await db.Prescriptions.AnyAsync(p=>p.Id==first.Id && !p.IsCancelled && p.Version==1) && await db.Invoices.AnyAsync(i=>i.Id==first.InvoiceId && i.TotalAmount==11000),"Audit fault rolls back cancellation and invoice");
  using(var r=await client.PostAsJsonAsync(cancelPath,new PrescriptionCancellationRequest(1))) { var result=await r.Content.ReadFromJsonAsync<PrescriptionCancellationResult>();check(r.StatusCode==HttpStatusCode.OK && result!.PrescriptionId==first.Id && result.Version==2 && result.InvoiceTotal==10000 && r.Headers.CacheControl?.NoStore==true,"Cancel200 removes medicine charges atomically with NoStore"); }
  check(await db.Visits.AnyAsync(v=>v.VisitId==id && v.Status==VisitStatuses.InProgress && !v.IsLocked) && await db.Invoices.AnyAsync(i=>i.Id==first.InvoiceId && i.Status==InvoiceStatus.Draft && i.TotalAmount==10000) && !await db.InvoiceItems.AnyAsync(i=>i.InvoiceId==first.InvoiceId && i.ItemType=="Medicine"),"Cancel keeps Visit and Draft identity plus service charges");
  check(oldItems.SequenceEqual(await db.PrescriptionItems.AsNoTracking().Where(i=>i.PrescriptionId==first.Id).Select(i=>new { i.Id,i.MedicineName,i.Quantity,i.UsageInstructions,i.UnitPrice }).ToListAsync()),"Canceled prescription item snapshots retained exactly");
  using(var r=await client.GetAsync(path)) { var state=await r.Content.ReadFromJsonAsync<PrescriptionContext>();check(state!.Prescription is null && state.CanEdit && !state.CanCancel,"No active order after cancellation; same Visit can prescribe anew"); }
  using(var r=await client.GetAsync(path+"/lookup-link"))check(r.StatusCode==HttpStatusCode.NotFound,"No lookup link until replacement saved404");
  using(var r=await client.GetAsync($"/api/public/prescriptions/{first.PrescriptionCode}"))check(r.StatusCode==HttpStatusCode.NotFound,"Old QR metadata revoked404");
  using(var r=await client.GetAsync($"/api/public/prescriptions/{first.PrescriptionCode}/details"))check(r.StatusCode==HttpStatusCode.Forbidden,"Existing token cannot read canceled old order403");client.DefaultRequestHeaders.Remove("X-Prescription-View-Token");
  using(var r=await client.PostAsJsonAsync(path,oldPreview))check(r.StatusCode==HttpStatusCode.Conflict,"Old create replay after cancel cannot create unwanted replacement409");
  using(var r=await client.PostAsJsonAsync(cancelPath,new PrescriptionCancellationRequest(1)))check(r.StatusCode==HttpStatusCode.Conflict,"Cancel replay after no active prescription409");
  var master=await db.Medicines.FindAsync(med.Id);master!.UnitPrice=700;master.Version=2;await db.SaveChangesAsync();med=master;db.ChangeTracker.Clear();
  var invoiceId=first.InvoiceId;var next=await Create(id);
  check(next.Items.Single().UnitPrice==700 && await db.PrescriptionItems.AnyAsync(i=>i.PrescriptionId==first.Id && i.UnitPrice==500),"Replacement uses current master price without rewriting old price");
  check(next.Id!=first.Id && next.PrescriptionCode!=first.PrescriptionCode && next.Version==1 && next.InvoiceId==invoiceId && await db.Prescriptions.CountAsync(p=>p.VisitId==id)==2 && await db.Prescriptions.CountAsync(p=>p.VisitId==id && !p.IsCancelled)==1,"Replacement new identity/code; one active and preserved old history");
  check(await db.Invoices.AnyAsync(i=>i.Id==invoiceId && i.TotalAmount==11400) && await db.InvoiceItems.AnyAsync(i=>i.InvoiceId==invoiceId && i.SourcePrescriptionItemId==next.Items.Single().Id) && !await db.InvoiceItems.AnyAsync(i=>i.InvoiceId==invoiceId && i.SourcePrescriptionItemId==first.Items.Single().Id),"Same invoice uses only new prescription item IDs");
  using(var r=await client.PostAsJsonAsync(cancelPath,new PrescriptionCancellationRequest(1)))check(r.StatusCode==HttpStatusCode.Conflict,"Old cancellation cannot cancel replacement with equal version409");
  using(var r=await client.GetAsync(path+"/lookup-link"))check((await r.Content.ReadFromJsonAsync<PrescriptionLookupLink>())!.PrescriptionCode==next.PrescriptionCode,"Lookup points only to replacement");
  foreach(var actor in new[] { (RoleCodes.Admin,admin.UserId),(RoleCodes.Receptionist,cashierId),(RoleCodes.Assistant,assistant.UserId),(RoleCodes.Patient,owner.UserId),(RoleCodes.Dentist,doctor.UserId) })
  {
   authenticate(actor.Item1,actor.Item2);using var r=await client.GetAsync(path+"/history?page=1&pageSize=1");var page=await r.Content.ReadFromJsonAsync<PagedResult<PrescriptionResponse>>();check(r.StatusCode==HttpStatusCode.OK && page!.TotalCount==2 && page.Items.Single().Id==next.Id && page.HasNextPage && r.Headers.CacheControl?.NoStore==true,"Read-authorized actor sees paged history with ownership");
  }
  using(var r=await client.GetAsync(path+"/history?page=2&pageSize=1"))check((await r.Content.ReadFromJsonAsync<PagedResult<PrescriptionResponse>>())!.Items.Single().IsCancelled,"Old canceled order visible on history page2");
  using(var r=await client.GetAsync(path+"/history?page=0"))check(r.StatusCode==HttpStatusCode.BadRequest,"History invalid page400");
  authenticate(RoleCodes.Patient,outsider.UserId);using(var r=await client.GetAsync(path+"/history"))check(r.StatusCode==HttpStatusCode.NotFound,"Foreign patient history404");
  // Database index is independently enforced even if an application caller bypasses the service.
  var uniqueBlocked=false;await using(var ctx=new DentalDbContext(options)) { ctx.Prescriptions.Add(new() { VisitId=id,DoctorId=doctor.UserId,CreatedByUserId=doctor.UserId,PrescriptionCode="TEST-UNIQUE",Diagnosis="Synthetic",CreatedAt=clock.UtcNow });try { await ctx.SaveChangesAsync(); }catch(DbUpdateException e)when(e.InnerException is PostgresException { SqlState: "23505" }) { uniqueBlocked=true; } }
  check(uniqueBlocked,"PostgreSQL filtered unique index rejects second effective order");
  var immutable=false;await using(var ctx=new DentalDbContext(options)) { var old=await ctx.Prescriptions.FindAsync(first.Id);old!.IsCancelled=false;try { await ctx.SaveChangesAsync(); }catch(InvalidOperationException) { immutable=true; } }check(immutable,"Canceled order cannot be resurrected directly");
  // First cancellation can commit at most once under contention.
  async Task<bool> CancelRace() { await using var ctx=new DentalDbContext(options);return(await Repo(ctx).CancelAsync(id,next.Id,doctor.UserId,new(1),null,default)).IsSuccess; }
  check((await Task.WhenAll(CancelRace(),CancelRace())).Count(ok=>ok)==1,"Concurrent cancels one winner");
  var racePreview=await Repo(db).PreviewAsync(id,doctor.UserId,Request(),default);var raceRequest=Request() with { PreviewToken=racePreview.Value!.Token };
  async Task<bool> CreateRace() { await using var ctx=new DentalDbContext(options);return(await Repo(ctx).SaveAsync(id,doctor.UserId,true,raceRequest,null,default)).IsSuccess; }
  check((await Task.WhenAll(CreateRace(),CreateRace())).Count(ok=>ok)==1 && await db.Prescriptions.CountAsync(p=>p.VisitId==id && !p.IsCancelled)==1,"Concurrent replacements one winner/new daily code");
  var last=await db.Prescriptions.AsNoTracking().SingleAsync(p=>p.VisitId==id && !p.IsCancelled);
  // Editing and canceling the same version cannot both win.
  var edit=Request() with { ExpectedVersion=last.Version,Items=[new(med.Id,med.Version,3,"Synthetic changed usage")] };var editPreview=await Repo(db).PreviewAsync(id,doctor.UserId,edit,default);edit=edit with { PreviewToken=editPreview.Value!.Token };
  async Task<bool> EditRace() { await using var ctx=new DentalDbContext(options);return(await Repo(ctx).SaveAsync(id,doctor.UserId,false,edit,null,default)).IsSuccess; }
  async Task<bool> FinalCancel() { await using var ctx=new DentalDbContext(options);return(await Repo(ctx).CancelAsync(id,last.Id,doctor.UserId,new(last.Version),null,default)).IsSuccess; }
  check((await Task.WhenAll(EditRace(),FinalCancel())).Count(ok=>ok)==1,"Concurrent edit versus cancellation one winner");
  var stateAfter=await db.Prescriptions.AsNoTracking().SingleAsync(p=>p.Id==last.Id);var total=await db.Invoices.Where(i=>i.Id==invoiceId).Select(i=>i.TotalAmount).SingleAsync();check(total==(stateAfter.IsCancelled?10000:12100),"Race invoice agrees with effective prescription only");
  authenticate(RoleCodes.Dentist,doctor.UserId);
  // A cancellation reducing total below an existing fixed discount fails without rewriting either record.
  var discountVisit=await Visit();var discountRx=await Create(discountVisit);var inv=await db.Invoices.SingleAsync(i=>i.Id==discountRx.InvoiceId);inv.DiscountAmount=10500;await db.SaveChangesAsync();db.ChangeTracker.Clear();
  using(var r=await client.PostAsJsonAsync(Path(discountVisit)+$"/{discountRx.Id}/cancel",new PrescriptionCancellationRequest(1)))check(r.StatusCode==HttpStatusCode.Conflict,"Excess discount blocks cancellation409");
  check(await db.Prescriptions.AnyAsync(p=>p.Id==discountRx.Id && !p.IsCancelled && p.Version==1) && await db.Invoices.AnyAsync(i=>i.Id==discountRx.InvoiceId && i.TotalAmount==11400 && i.DiscountAmount==10500),"Discount conflict rolls back flag/version/invoice without adjusting discount");
  foreach(var status in new[] { InvoiceStatus.PendingPayment,InvoiceStatus.PartiallyPaid,InvoiceStatus.Paid })
  { inv=await db.Invoices.SingleAsync(i=>i.Id==discountRx.InvoiceId);inv.Status=status;await db.SaveChangesAsync();db.ChangeTracker.Clear();using var r=await client.PostAsJsonAsync(Path(discountVisit)+$"/{discountRx.Id}/cancel",new PrescriptionCancellationRequest(1));check(r.StatusCode==HttpStatusCode.Conflict,"Non-Draft status cannot cancel409"); }
  inv=await db.Invoices.SingleAsync(i=>i.Id==discountRx.InvoiceId);inv.Status=InvoiceStatus.Draft;inv.DiscountAmount=0;await db.SaveChangesAsync();db.ChangeTracker.Clear();
  foreach(var status in new[] { VisitStatuses.Correcting,VisitStatuses.Completed,VisitStatuses.Cancelled })
  { var v=await db.Visits.FindAsync(discountVisit);v!.Status=status;await db.SaveChangesAsync();db.ChangeTracker.Clear();using var r=await client.PostAsJsonAsync(Path(discountVisit)+$"/{discountRx.Id}/cancel",new PrescriptionCancellationRequest(1));check(r.StatusCode==HttpStatusCode.Conflict,"Non-InProgress Visit cannot cancel409"); }
  var resetVisit=await db.Visits.FindAsync(discountVisit);resetVisit!.Status=VisitStatuses.InProgress;await db.SaveChangesAsync();db.ChangeTracker.Clear();
  db.PaymentTransactions.Add(new() { InvoiceId=discountRx.InvoiceId!.Value,Amount=1,AmountTendered=1,ChangeAmount=0,CashierId=cashierId,PaidAt=clock.UtcNow,RequestId=Guid.NewGuid(),Source="CASH" });await db.SaveChangesAsync();db.ChangeTracker.Clear();
  using(var r=await client.PostAsJsonAsync(Path(discountVisit)+$"/{discountRx.Id}/cancel",new PrescriptionCancellationRequest(1)))check(r.StatusCode==HttpStatusCode.Conflict,"Historical receipt blocks cancellation even when PaidAmount reset0");
  // Existing visit cancellation must work with multiple historical prescriptions.
  await using(var ctx=new DentalDbContext(options)) { await using var tx=await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable);var r=await new InvoiceRepository(ctx).StageVisitCancellationAsync(id,doctor.UserId,RoleCodes.Dentist,null);if(r.IsSuccess) { var v=await ctx.Visits.FindAsync(id);v!.Status=VisitStatuses.Cancelled;await ctx.SaveChangesAsync();await tx.CommitAsync(); }check(r.IsSuccess,"Visit cancellation supports multiple historical prescriptions"); }
  check(!await db.Prescriptions.AnyAsync(p=>p.VisitId==id && !p.IsCancelled),"Visit cancellation leaves no effective prescription");
  var original=await db.PrescriptionItems.AsNoTracking().Where(i=>i.PrescriptionId==first.Id).Select(i=>new { i.Id,i.MedicineName,i.Quantity,i.UsageInstructions,i.UnitPrice }).ToListAsync();check(oldItems.SequenceEqual(original),"Canceled original snapshots survive replacement edits and Visit cancellation");
  check(await db.AuditLogs.CountAsync(a=>a.Action=="MOD_RX_PRESCRIPTION_CANCELLED" && a.EntityId==first.Id)==1 && await db.AuditLogs.AllAsync(a=>a.Detail==null || !a.Detail.Contains("Synthetic doctor instruction")),"Cancel audit once, contains no clinical text");
  db.ChangeTracker.Clear();var actual=await db.Users.FindAsync(doctor.UserId);actual!.IsActive=false;await db.SaveChangesAsync();db.ChangeTracker.Clear();
  using(var r=await client.PostAsJsonAsync(Path(discountVisit)+$"/{discountRx.Id}/cancel",new PrescriptionCancellationRequest(1)))check(r.StatusCode==HttpStatusCode.NotFound,"Disabled doctor stale JWT cannot cancel404");
  using(var r=await client.GetAsync(path+"/history"))check(r.StatusCode==HttpStatusCode.NotFound,"Disabled doctor history404");
 }
 private sealed class FailCancellationAudit : SaveChangesInterceptor
 {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
  { if(data.Context!.ChangeTracker.Entries<AuditLog>().Any(x=>x.Entity.Action=="MOD_RX_PRESCRIPTION_CANCELLED"))throw new InvalidOperationException("Synthetic cancel audit fault");return ValueTask.FromResult(result); }
 }
}
