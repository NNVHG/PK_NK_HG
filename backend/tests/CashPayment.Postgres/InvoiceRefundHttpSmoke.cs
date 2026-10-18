using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
internal static class InvoiceRefundHttpSmoke
{
 public static async Task RunAsync(HttpClient client,DbContextOptions<DentalDbContext> options,int cashierId,Action<string,int> auth,Action<bool,string> check)
 {
  await using var db=new DentalDbContext(options);var clock=new VietnamClock(SmokeTestTimeProvider.Instance);var seq=0;
  async Task<User> Actor(string code)
  {
   var role=await db.Roles.SingleOrDefaultAsync(r=>r.RoleCode==code);if(role is null){role=new(){RoleCode=code,RoleName=code};db.Roles.Add(role);await db.SaveChangesAsync();}
   var u=new User{RoleId=role.RoleId,FullName="Synthetic refund actor "+(++seq),Phone="097"+seq.ToString("D7"),PasswordHash=BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString())};db.Users.Add(u);await db.SaveChangesAsync();return u;
  }
  var admin=await Actor(RoleCodes.Admin);var otherAdmin=await Actor(RoleCodes.Admin);var doctor=await Actor(RoleCodes.Dentist);var assistant=await Actor(RoleCodes.Assistant);var patientUser=await Actor(RoleCodes.Patient);
  var patient=new Patient{FullName="Synthetic refund patient",Phone="0998880001",DateOfBirth=clock.Today.AddYears(-30),UserId=patientUser.UserId};db.Patients.Add(patient);
  var med=new Medicine{Code="REFUND-SYNTHETIC",Name="Synthetic medicine",Unit="unit",UnitPrice=200000};db.Medicines.Add(med);await db.SaveChangesAsync();
  async Task<(Invoice Invoice,Prescription? Rx)> Order(decimal? paid=null,string? discountType=null,decimal discount=0,bool medicine=false,double paidDays=0,int? collectionActor=null)
  {
   var visit=new Visit{PatientId=patient.PatientId,DentistId=doctor.UserId,CreatedByUserId=cashierId,Status=VisitStatuses.Completed,IsLocked=true};
   var i=new Invoice{Visit=visit,InvoiceCode="REFUND-"+(++seq),CreatedByUserId=cashierId,Status=InvoiceStatus.PendingPayment,CreatedAt=clock.UtcNow,TotalAmount=medicine?1200000:1000000,
    DiscountType=discountType,DiscountValue=discountType is null?null:discount,DiscountAmount=discountType=="Percentage"?Math.Round(1000000*discount/100,MidpointRounding.AwayFromZero):discount,
    Items=[new(){Code="A",Name="Synthetic service A",Quantity=1,UnitPrice=500000,TotalAmount=500000},new(){Code="B",Name="Synthetic service B",Quantity=1,UnitPrice=500000,TotalAmount=500000}]};
   db.Invoices.Add(i);await db.SaveChangesAsync();Prescription? rx=null;
   if(medicine)
   {
    rx=new Prescription{Visit=visit,Invoice=i,DoctorId=doctor.UserId,CreatedByUserId=doctor.UserId,PrescriptionCode="RX-20261018-"+(8000+seq),CreatedAt=clock.UtcNow,Diagnosis="Saved synthetic diagnosis",
     Items=[new(){MedicineId=med.Id,MedicineCode=med.Code,MedicineName=med.Name,Unit="unit",Quantity=1,UnitPrice=200000,UsageInstructions="Saved clinician directions"}]};
    db.Prescriptions.Add(rx);await db.SaveChangesAsync();i.Items.Add(new(){ItemType="Medicine",Code=med.Code,Name=med.Name,Quantity=1,UnitPrice=200000,TotalAmount=200000,SourcePrescriptionItemId=rx.Items.Single().Id});await db.SaveChangesAsync();
   }
   i.PaidAmount=paid??i.FinalAmount;i.Status=i.PaidAmount==i.FinalAmount?InvoiceStatus.Paid:InvoiceStatus.PartiallyPaid;
   if(i.PaidAmount>0)db.PaymentTransactions.Add(new(){Invoice=i,Amount=i.PaidAmount,PaymentMethod="Cash",CashierId=collectionActor??admin.UserId,PaidAt=clock.UtcNow.AddDays(-paidDays),RequestId=Guid.NewGuid()});
   await db.SaveChangesAsync();db.ChangeTracker.Clear();return(i,rx);
  }
  string Url(int id)=>$"/api/invoices/{id}/refund";
  async Task<InvoiceRefundContext> Read(int id){using var r=await client.GetAsync(Url(id));check(r.StatusCode==HttpStatusCode.OK,"Admin reads refund snapshot200");check(r.Headers.CacheControl?.NoStore==true,"Refund medical/financial context NoStore");return(await r.Content.ReadFromJsonAsync<InvoiceRefundContext>())!;}
  async Task<InvoiceRefundRequest> Request(int id,string code="A",int quantity=1,string method="Cash",bool approvals=true)
  {
   var c=await Read(id);using var response=await client.PostAsJsonAsync(Url(id)+"/preview",new InvoiceRefundPreviewRequest(c.Snapshot,[new(c.Items.Single(x=>x.Code==code).Id,quantity)],method));
   check(response.StatusCode==HttpStatusCode.OK,"Admin previews server-calculated refund200");var v=(await response.Content.ReadFromJsonAsync<InvoiceRefundPreview>())!;
   return new(c.Snapshot,[new(c.Items.Single(x=>x.Code==code).Id,quantity)],method,v.Preview,Guid.NewGuid(),"OTHER","Synthetic refund adjustment",true,approvals&&v.RequiresLateApproval,approvals&&v.RequiresReserve);
  }
  async Task<RefundRecordDto> Apply(int id,InvoiceRefundRequest request)
  {
   using var r=await client.PostAsJsonAsync(Url(id),request);check(r.StatusCode==HttpStatusCode.OK,"Admin confirms refund200");return(await r.Content.ReadFromJsonAsync<RefundRecordDto>())!;
  }
  var seed=await Order();client.DefaultRequestHeaders.Authorization=null;using(var r=await client.GetAsync(Url(seed.Invoice.Id)))check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous refund read401");
  foreach(var who in new[]{(RoleCodes.Receptionist,cashierId),(RoleCodes.Dentist,doctor.UserId),(RoleCodes.Assistant,assistant.UserId),(RoleCodes.Patient,patientUser.UserId)})
  {
   auth(who.Item1,who.Item2);using(var r=await client.GetAsync(Url(seed.Invoice.Id)))check(r.StatusCode==HttpStatusCode.Forbidden,"NonAdmin refund context403");
   using(var r=await client.PostAsJsonAsync(Url(seed.Invoice.Id),new{confirmed=true}))check(r.StatusCode==HttpStatusCode.Forbidden,"NonAdmin cannot create refund403");
  }
  auth(RoleCodes.Admin,admin.UserId);var request=await Request(seed.Invoice.Id);using(var r=await client.PostAsJsonAsync(Url(seed.Invoice.Id),request with{Confirmed=false}))check(r.StatusCode==HttpStatusCode.BadRequest,"Explicit confirmation required400");
  using(var r=await client.PostAsJsonAsync(Url(seed.Invoice.Id),request with{ReasonNotes=""}))check(r.StatusCode==HttpStatusCode.BadRequest,"Refund notes required400");
  using(var r=await client.PostAsJsonAsync(Url(seed.Invoice.Id),request with{ReasonCode="BAD"}))check(r.StatusCode==HttpStatusCode.BadRequest,"Reason codes enforced400");
  var first=await Apply(seed.Invoice.Id,request);check(first.RefundAmount=="500000" && first.DebtReduction=="0" && first.PaidAfter=="500000" && first.VoucherCode!.StartsWith("PC-"),"Paid refund reduces cash and preserves settled remainder");
  check(await db.PaymentTransactions.CountAsync(t=>t.InvoiceId==seed.Invoice.Id)==2 && await db.PaymentTransactions.AnyAsync(t=>t.Id==first.PaymentId && t.Amount==-500000 && t.TransactionType=="Refund" && t.RefundReasonCode=="OTHER"),"One new negative payment with reason; original retained");
  var again=await Apply(seed.Invoice.Id,request);check(again.Id==first.Id && await db.InvoiceRefunds.CountAsync(r=>r.InvoiceId==seed.Invoice.Id)==1,"Identical retry idempotent, one adjustment");
  using(var r=await client.PostAsJsonAsync(Url(seed.Invoice.Id),request with{ReasonNotes="Different reason"}))check(r.StatusCode==HttpStatusCode.Conflict,"Changed payload same RequestId rejected409");
  auth(RoleCodes.Admin,otherAdmin.UserId);using(var r=await client.PostAsJsonAsync(Url(seed.Invoice.Id),request))check(r.StatusCode==HttpStatusCode.Conflict,"RequestId bound to original Admin409");auth(RoleCodes.Admin,admin.UserId);
  var after=await Read(seed.Invoice.Id);check(after.Status==5 && after.Items.Single(x=>x.Code=="A").RefundedQuantity==1 && after.Items.Single(x=>x.Code=="A").OriginalQuantity==1,"Original quantity retained; refund quantity projected separately");
  using(var r=await client.PostAsJsonAsync(Url(seed.Invoice.Id)+"/preview",new InvoiceRefundPreviewRequest(after.Snapshot,[new(after.Items.Single(x=>x.Code=="A").Id,1)],"Cash")))check(r.StatusCode==HttpStatusCode.Conflict,"Already returned line cannot refund again409");
  using(var r=await client.PostAsJsonAsync(Url(seed.Invoice.Id)+$"/{first.Id}/print",new{})){var v=(await r.Content.ReadFromJsonAsync<InvoiceRefundVoucher>())!;check(r.StatusCode==HttpStatusCode.OK && v.Record.RefundAmount=="500000" && v.PrintNumber==1 && v.PatientName==patient.FullName,"Protected K80 voucher uses actual refund and patient");}
  using(var r=await client.PostAsJsonAsync(Url(seed.Invoice.Id)+$"/{first.Id}/print",new{})){var v=(await r.Content.ReadFromJsonAsync<InvoiceRefundVoucher>())!;check(v.PrintNumber==2,"Reprint increments metadata only");}
  using(var r=await client.GetAsync($"/api/invoices/{seed.Invoice.Id}/payments/{first.PaymentId}/receipt"))check(r.StatusCode!=HttpStatusCode.OK,"Negative payment cannot become positive receipt");
  var partial=await Order(paid:600000);var partialResult=await Apply(partial.Invoice.Id,await Request(partial.Invoice.Id));check(partialResult.RefundAmount=="100000" && partialResult.DebtReduction=="400000" && partialResult.RemainingAfter=="0","Q323a600k paid cancel500k refunds100k and clears400k debt");
  var debt=await Order(paid:200000);var debtResult=await Apply(debt.Invoice.Id,await Request(debt.Invoice.Id));check(debtResult.RefundAmount=="0" && debtResult.PaymentId is null && debtResult.VoucherCode is null && debtResult.RemainingAfter=="300000","Debt-only adjustment creates no zero payment or voucher");
  check(await db.PaymentTransactions.CountAsync(t=>t.InvoiceId==debt.Invoice.Id)==1,"Only original payment remains on debt-only cancellation");
  using(var ctx=new DentalDbContext(options)){var paid=await new CashPaymentRepository(ctx).ReceiveAsync(debt.Invoice.Id,cashierId,new(300000,300000,Guid.NewGuid(),"Cash"),null);check(paid.IsSuccess,"Cash collection continues on refunded outstanding debt");}
  check(await db.Invoices.AnyAsync(i=>i.Id==debt.Invoice.Id && i.Status==InvoiceStatus.PartiallyRefunded && i.PaidAmount==500000),"Repayment preserves refund history status");

  var originalPayment=await db.PaymentTransactions.Where(t=>t.InvoiceId==seed.Invoice.Id && t.Amount>0).Select(t=>t.Id).SingleAsync();using(var r=await client.GetAsync($"/api/invoices/{seed.Invoice.Id}/payments/{originalPayment}/receipt")){var receipt=(await r.Content.ReadFromJsonAsync<ReceiptPrintDto>())!;check(r.StatusCode==HttpStatusCode.OK && receipt.Items.Count==1 && receipt.Items.Sum(x=>x.TotalAmount)==500000 && receipt.Amount==1000000,"Original positive receipt retains payment amount and projects remaining invoice lines");}
  using(var r=await client.GetAsync($"/api/invoices/by-visit/{seed.Invoice.VisitId}")){var invoice=(await r.Content.ReadFromJsonAsync<InvoiceResponse>())!;check(r.StatusCode==HttpStatusCode.OK && invoice.TotalAmount==500000 && invoice.Items.Sum(x=>x.RefundedQuantity)==1,"Invoice read retains original lines and exposes returned quantity");}
  var full=await Apply(seed.Invoice.Id,await Request(seed.Invoice.Id,"B"));check(full.RefundAmount=="500000" && full.TotalAfter=="0" && full.PaidAfter=="0","Second cancellation completes full refund");check(await db.Invoices.AnyAsync(i=>i.Id==seed.Invoice.Id && i.Status==InvoiceStatus.Refunded),"Full cancellation ends Refunded not Cancelled");
  var fixedOrder=await Order(discountType:"FixedAmount",discount:100000);var fixedResult=await Apply(fixedOrder.Invoice.Id,await Request(fixedOrder.Invoice.Id));check(fixedResult.RefundAmount=="500000" && fixedResult.DiscountAfter=="100000" && fixedResult.PaidAfter=="400000","DL300 retains original fixed discount on remainder");
  var percent=await Order(discountType:"Percentage",discount:10,medicine:true);var percentResult=await Apply(percent.Invoice.Id,await Request(percent.Invoice.Id));check(percentResult.DiscountAfter=="50000" && percentResult.RefundAmount=="450000" && percentResult.PaidAfter=="650000","Percentage discount remains service-only after refund");
  var over=await Order(discountType:"FixedAmount",discount:900000);var oc=await Read(over.Invoice.Id);using(var r=await client.PostAsJsonAsync(Url(over.Invoice.Id)+"/preview",new InvoiceRefundPreviewRequest(oc.Snapshot,[new(oc.Items[0].Id,1)],"Cash")))check(r.StatusCode==HttpStatusCode.Conflict,"Fixed original discount greater than remainder blocks partial refund");

  var stale=await Order();var sr=await Request(stale.Invoice.Id);await Apply(stale.Invoice.Id,sr with{RequestId=Guid.NewGuid()});using(var r=await client.PostAsJsonAsync(Url(stale.Invoice.Id),sr))check(r.StatusCode==HttpStatusCode.Conflict,"Stale snapshot409");
  using(var r=await client.PostAsJsonAsync(Url(over.Invoice.Id),request))check(r.StatusCode==HttpStatusCode.Conflict,"Request cannot cross invoice409");
  var late=await Order(paidDays:8);var lr=await Request(late.Invoice.Id,approvals:false);using(var r=await client.PostAsJsonAsync(Url(late.Invoice.Id),lr))check(r.StatusCode==HttpStatusCode.Conflict,"Past7days requires special approval");await Apply(late.Invoice.Id,lr with{LateApproval=true,ReserveConfirmed=true});
  var reserve=await Order(collectionActor:cashierId);auth(RoleCodes.Admin,otherAdmin.UserId);var rr=await Request(reserve.Invoice.Id,approvals:false);using(var r=await client.PostAsJsonAsync(Url(reserve.Invoice.Id),rr))check(r.StatusCode==HttpStatusCode.Conflict,"Current Admin daily net cash requires reserve confirmation");await Apply(reserve.Invoice.Id,rr with{ReserveConfirmed=true});auth(RoleCodes.Admin,admin.UserId);
  var bank=await Order();var bankResult=await Apply(bank.Invoice.Id,await Request(bank.Invoice.Id,method:"BankTransfer"));check(bankResult.PaymentMethod=="BankTransfer" && await db.PaymentTransactions.AnyAsync(t=>t.Id==bankResult.PaymentId && t.Amount==-500000 && t.PaymentMethod=="BankTransfer"),"Refund can record separate negative bank transfer");
  var fault=await Order();var fr=await Request(fault.Invoice.Id);var faultOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;var failed=false;
  await using(var ctx=new DentalDbContext(faultOptions))try{await new InvoiceRefundRepository(ctx,clock).RefundAsync(fault.Invoice.Id,admin.UserId,fr,null,default);}catch(InvalidOperationException e)when(e.Message=="Synthetic refund audit failure"){failed=true;}
  check(failed && !await db.InvoiceRefunds.AnyAsync(r=>r.InvoiceId==fault.Invoice.Id) && await db.Invoices.AnyAsync(i=>i.Id==fault.Invoice.Id && i.TotalAmount==1000000 && i.PaidAmount==1000000 && i.RefundRevision==0) && await db.PaymentTransactions.CountAsync(t=>t.InvoiceId==fault.Invoice.Id)==1,"Audit failure rolls back all financial and line mutations");
  var race=await Order();var raceRequest=await Request(race.Invoice.Id);async Task<bool> Race(){await using var ctx=new DentalDbContext(options);return(await new InvoiceRefundRepository(ctx,clock).RefundAsync(race.Invoice.Id,admin.UserId,raceRequest with{RequestId=Guid.NewGuid()},null,default)).IsSuccess;}
  check((await Task.WhenAll(Race(),Race())).Count(x=>x)==1 && await db.InvoiceRefunds.CountAsync(r=>r.InvoiceId==race.Invoice.Id)==1,"Concurrent refunds exactly one commit");
  var service=await Order(medicine:true);await Apply(service.Invoice.Id,await Request(service.Invoice.Id));auth(RoleCodes.Assistant,assistant.UserId);
  using(var r=await client.GetAsync($"/api/prescriptions/{service.Rx!.Id}/dispense")){var status=(await r.Content.ReadFromJsonAsync<PrescriptionHandoverStatus>())!;check(status.CanDispense,"Q322 service-only refund allows settled unrefunded medicine");}
  using(var r=await client.PostAsJsonAsync($"/api/prescriptions/{service.Rx!.Id}/dispense",new PrescriptionDispenseRequest(1,true)))check(r.StatusCode==HttpStatusCode.OK,"Assistant delivers on PartiallyRefunded200");
  auth(RoleCodes.Admin,admin.UserId);var c=await Read(service.Invoice.Id);using(var r=await client.PostAsJsonAsync(Url(service.Invoice.Id)+"/preview",new InvoiceRefundPreviewRequest(c.Snapshot,[new(c.Items.Single(x=>x.ItemType=="Medicine").Id,1)],"Cash")))check(r.StatusCode==HttpStatusCode.Conflict,"Cannot refund dispensed medicine409");
  using(var r=await client.PostAsJsonAsync($"/api/prescriptions/{service.Rx!.Id}/undo-dispense",new PrescriptionUndoDispenseRequest(2,true,"Synthetic undo correction")))check(r.StatusCode==HttpStatusCode.OK,"Undo still allowed within30minutes after service refund");
  await Apply(service.Invoice.Id,await Request(service.Invoice.Id,med.Code));auth(RoleCodes.Assistant,assistant.UserId);using(var r=await client.GetAsync($"/api/prescriptions/{service.Rx.Id}/dispense")){var status=(await r.Content.ReadFromJsonAsync<PrescriptionHandoverStatus>())!;check(!status.CanDispense && !status.CanOverrideExpired,"Any refunded medicine prevents original whole prescription delivery");}
  using(var r=await client.PostAsJsonAsync($"/api/prescriptions/{service.Rx.Id}/dispense",new PrescriptionDispenseRequest(3,true)))check(r.StatusCode==HttpStatusCode.Conflict,"Refunded medicine delivery409");
  auth(RoleCodes.Admin,admin.UserId);var unpaid=await Order(paid:100000,medicine:true);await Apply(unpaid.Invoice.Id,await Request(unpaid.Invoice.Id));auth(RoleCodes.Assistant,assistant.UserId);using(var r=await client.GetAsync($"/api/prescriptions/{unpaid.Rx!.Id}/dispense")){var status=(await r.Content.ReadFromJsonAsync<PrescriptionHandoverStatus>())!;check(!status.CanDispense,"Refund status cannot waive remaining debt");}
  auth(RoleCodes.Admin,admin.UserId);var concurrent=await Order(medicine:true);var cr=await Request(concurrent.Invoice.Id,med.Code);async Task<bool> Cancel(){await using var ctx=new DentalDbContext(options);return(await new InvoiceRefundRepository(ctx,clock).RefundAsync(concurrent.Invoice.Id,admin.UserId,cr,null,default)).IsSuccess;}async Task<bool> Deliver(){await using var ctx=new DentalDbContext(options);return(await new PrescriptionHandoverRepository(ctx,clock).DispenseAsync(concurrent.Rx!.Id,assistant.UserId,RoleCodes.Assistant,new(1,true),null,default)).IsSuccess;}
  check((await Task.WhenAll(Cancel(),Deliver())).Count(x=>x)==1,"Serializable medicine refund and delivery cannot both commit");
  var free=await Order(paid:0,discountType:"FixedAmount",discount:1000000);var fc=await Read(free.Invoice.Id);var all=fc.Items.Select(x=>new RefundSelection(x.Id,x.RemainingQuantity)).ToArray();using(var r=await client.PostAsJsonAsync(Url(free.Invoice.Id)+"/preview",new InvoiceRefundPreviewRequest(fc.Snapshot,all,"Cash"))){var v=(await r.Content.ReadFromJsonAsync<InvoiceRefundPreview>())!;check(r.StatusCode==HttpStatusCode.OK,"Free fully discounted cancellation preview200");var result=await Apply(free.Invoice.Id,new(fc.Snapshot,all,"Cash",v.Preview,Guid.NewGuid(),"OTHER","Synthetic free cancellation",true));check(result.PaymentId is null && !await db.PaymentTransactions.AnyAsync(t=>t.InvoiceId==free.Invoice.Id),"Free cancellation creates no fictitious payment");}

  auth(RoleCodes.Admin,admin.UserId);
  var report=await new RevenueReportRepository(db).ReadAsync(new(clock.Today,clock.Today,"Day","All","Revenue",null),default);
  var start=DateTime.SpecifyKind(clock.Today.ToDateTime(TimeOnly.MinValue).AddHours(-7),DateTimeKind.Utc);
  var actualNet=await db.PaymentTransactions.Where(t=>t.PaidAt>=start && t.PaidAt<start.AddDays(1)).SumAsync(t=>t.Amount);
  var actualDebt=await db.Invoices.Where(i=>i.Status!=InvoiceStatus.Cancelled && i.Status!=InvoiceStatus.Draft).SumAsync(i=>i.TotalAmount-i.DiscountAmount>i.PaidAmount?i.TotalAmount-i.DiscountAmount-i.PaidAmount:0);
  check(report.Summary.NetRevenue==actualNet && report.Summary.TotalRefunded>0,"Revenue query includes real negative refund transactions");
  check(report.Summary.TotalOutstanding==actualDebt,"Current outstanding includes remaining debt on PartiallyRefunded");
  var transferDebt=await Order(paid:100000);await Apply(transferDebt.Invoice.Id,await Request(transferDebt.Invoice.Id));using(var r=await client.GetAsync($"/api/invoices/{transferDebt.Invoice.Id}/bank-transfer-info"))check(r.StatusCode==HttpStatusCode.OK,"Transfer info supports remaining debt after refund");
  var mixed=await Order(paid:600000,paidDays:8);await using(var ctx=new DentalDbContext(options)){var received=await new CashPaymentRepository(ctx).ReceiveAsync(mixed.Invoice.Id,cashierId,new(400000,400000,Guid.NewGuid()),null);check(received.IsSuccess,"Split-payment fixture collects remaining balance");}
  var mc=await Read(mixed.Invoice.Id);using(var r=await client.PostAsJsonAsync(Url(mixed.Invoice.Id)+"/preview",new InvoiceRefundPreviewRequest(mc.Snapshot,[new(mc.Items[0].Id,1)],"Cash")))check(r.StatusCode==HttpStatusCode.Conflict && (await r.Content.ReadAsStringAsync()).Contains("BIL_REFUND_WINDOW_UNRESOLVED"),"Q324 mixed old/new receipts are blocked before choosing a deadline");
  check(!await db.InvoiceRefunds.AnyAsync(r=>r.InvoiceId==mixed.Invoice.Id),"Unresolved deadline creates no refund or adjustment");
  db.ChangeTracker.Clear();var original=await db.InvoiceItems.FirstAsync(i=>i.InvoiceId==seed.Invoice.Id);original.Quantity=9;failed=false;try{await db.SaveChangesAsync();}catch(InvalidOperationException){failed=true;}check(failed,"Original invoice line immutable after payment/refund");db.ChangeTracker.Clear();
  var saved=await db.InvoiceRefunds.FindAsync(first.Id);saved!.ReasonNotes="Must not overwrite";failed=false;try{await db.SaveChangesAsync();}catch(InvalidOperationException){failed=true;}check(failed,"Refund history immutable");db.ChangeTracker.Clear();
  var line=await db.InvoiceRefundItems.FirstAsync(i=>i.InvoiceRefundId==first.Id);db.InvoiceRefundItems.Remove(line);failed=false;try{await db.SaveChangesAsync();}catch(InvalidOperationException){failed=true;}check(failed,"Refund line cannot be deleted");db.ChangeTracker.Clear();
  var payment=await db.PaymentTransactions.FindAsync(first.PaymentId);payment!.Amount=-1;failed=false;try{await db.SaveChangesAsync();}catch(InvalidOperationException){failed=true;}check(failed,"Negative payment financial fields immutable");db.ChangeTracker.Clear();
  var disabled=await db.Users.FindAsync(otherAdmin.UserId);disabled!.IsActive=false;await db.SaveChangesAsync();db.ChangeTracker.Clear();auth(RoleCodes.Admin,otherAdmin.UserId);using(var r=await client.GetAsync(Url(seed.Invoice.Id)))check(r.StatusCode==HttpStatusCode.Forbidden,"Inactive Admin stale JWT read403");using(var r=await client.PostAsJsonAsync(Url(seed.Invoice.Id),request))check(r.StatusCode==HttpStatusCode.Forbidden,"Inactive Admin stale JWT replay403");
  check(!await db.AuditLogs.AnyAsync(a=>a.Action.StartsWith("MOD_BIL_REFUND") && a.Detail!.Contains("Synthetic refund adjustment")),"Generic audit excludes patient/reason text");

 }
 private sealed class FailAudit:SaveChangesInterceptor
 {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
  {if(data.Context!.ChangeTracker.Entries<AuditLog>().Any(e=>e.State==EntityState.Added && e.Entity.Action=="MOD_BIL_REFUND_RECORDED"))throw new InvalidOperationException("Synthetic refund audit failure");return base.SavingChangesAsync(data,result,ct);}
 }
}
