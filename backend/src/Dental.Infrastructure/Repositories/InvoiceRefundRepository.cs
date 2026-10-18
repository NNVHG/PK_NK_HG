using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Dental.Infrastructure.Repositories;
public sealed class InvoiceRefundRepository(DentalDbContext db,VietnamClock clock) : IInvoiceRefundRepository
{
    private static readonly Error Changed=new("BIL_REFUND_CONFLICT","Dữ liệu đã thay đổi. Tải lại hóa đơn và xem trước lần nữa; không chi thêm tiền khi chưa đối chiếu lịch sử.");
    private static string Money(decimal value)=>value.ToString("0",CultureInfo.InvariantCulture);
    private static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private Task<bool> Admin(int actor,CancellationToken ct)=>db.Users.AnyAsync(u=>u.UserId==actor && u.IsActive && u.Role.RoleCode==RoleCodes.Admin,ct);
    private Task<Invoice?> Load(int id,CancellationToken ct)=>db.Invoices.Include(i=>i.Visit).Include(i=>i.Items)
        .Include(i=>i.Refunds).ThenInclude(r=>r.Items).AsSplitQuery().SingleOrDefaultAsync(i=>i.Id==id,ct);
    private static string Snapshot(Invoice i)=>InvoiceRefundSnapshot.Create(i);
    private static string PreviewKey(string snapshot,IReadOnlyList<RefundSelection> items,string method)=>Hash(new{snapshot,items=items.OrderBy(x=>x.InvoiceItemId),method});
    private static string Payload(InvoiceRefundRequest r)=>Hash(new{r.Snapshot,items=r.Items.OrderBy(x=>x.InvoiceItemId),r.PaymentMethod,r.ExpectedPreview,r.RequestId,r.ReasonCode,notes=r.ReasonNotes.Trim(),r.Confirmed,r.LateApproval,r.ReserveConfirmed});
    private static RefundRecordDto Map(InvoiceRefund r)=>new(r.Id,r.Revision,r.RequestId,r.PaymentTransactionId,
        r.PaymentTransactionId is int payment ? $"PC-{r.CreatedAt.AddHours(7):yyyyMMdd}-{payment}" : null,
        Money(r.RefundAmount),Money(r.DebtReduction),Money(r.TotalAfter),Money(r.DiscountAfter),Money(r.PaidAfter),Money(r.TotalAfter-r.DiscountAfter-r.PaidAfter),
        r.ReasonCode,r.ReasonNotes,r.PaymentMethod,r.CreatedAt,r.Actor.FullName);
    public async Task<Result<InvoiceRefundContext>> ReadAsync(int id,int actorId,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        if(!await Admin(actorId,ct))return Result<InvoiceRefundContext>.Failure(Error.Forbidden);
        var i=await Load(id,ct);if(i is null)return Result<InvoiceRefundContext>.Failure(Error.NotFound);
        var quantities=i.Refunds.SelectMany(r=>r.Items).GroupBy(x=>x.InvoiceItemId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));
        var records=await db.InvoiceRefunds.AsNoTracking().Include(r=>r.Actor).Where(r=>r.InvoiceId==id).OrderByDescending(r=>r.Revision).ToListAsync(ct);
        var result=new InvoiceRefundContext(id,i.InvoiceCode,(int)i.Status,Snapshot(i),Money(i.TotalAmount),Money(i.DiscountAmount),Money(i.PaidAmount),Money(i.FinalAmount-i.PaidAmount),
            i.Items.OrderBy(x=>x.Id).Select(x=>new RefundLineDto(x.Id,x.ItemType,x.Code,x.Name,x.Quantity,quantities.GetValueOrDefault(x.Id),x.Quantity-quantities.GetValueOrDefault(x.Id),Money(x.UnitPrice))).ToArray(),records.Select(Map).ToArray());
        await tx.CommitAsync(ct);return Result<InvoiceRefundContext>.Success(result);
    }
    private async Task<Result<(RefundCalculation Calculation,InvoiceRefundPreview Preview)>> Prepare(Invoice i,int actor,InvoiceRefundPreviewRequest request,CancellationToken ct)
    {
        if(Snapshot(i)!=request.Snapshot)return Result<(RefundCalculation,InvoiceRefundPreview)>.Failure(Changed);
        var result=InvoiceRefundRules.Calculate(i,request.Items);if(result.IsFailure)return Result<(RefundCalculation,InvoiceRefundPreview)>.Failure(result.Error);
        var money=await db.PaymentTransactions.Where(t=>t.InvoiceId==i.Id).SumAsync(t=>t.Amount,ct);
        if(money!=i.PaidAmount)return Result<(RefundCalculation,InvoiceRefundPreview)>.Failure(Changed);
        var selected=request.Items.Select(x=>x.InvoiceItemId).ToArray();
        var medicineIds=i.Items.Where(x=>selected.Contains(x.Id) && x.ItemType=="Medicine").Select(x=>x.SourcePrescriptionItemId).ToArray();
        if(medicineIds.Any(x=>x is null) || await db.PrescriptionItems.AnyAsync(x=>medicineIds.Contains(x.Id) && x.Prescription.DispensedAt!=null,ct))
            return Result<(RefundCalculation,InvoiceRefundPreview)>.Failure(new("BIL_REFUND_DISPENSED","Thuốc đã giao không được hoàn; phải hoàn tác giao thuốc hợp lệ trước."));
        var c=result.Value!;
        var start=DateTime.SpecifyKind(clock.Today.ToDateTime(TimeOnly.MinValue).AddHours(-7),DateTimeKind.Utc);
        var cash=await db.PaymentTransactions.Where(t=>t.CashierId==actor && t.PaymentMethod=="Cash" && t.PaidAt>=start && t.PaidAt<start.AddDays(1)).SumAsync(t=>t.Amount,ct);
        var receipts=await db.PaymentTransactions.Where(t=>t.InvoiceId==i.Id && t.Amount>0).Select(t=>t.PaidAt).ToListAsync(ct);
        var window=c.Refund>0 ? InvoiceRefundRules.RequiresLateApproval(receipts,clock.UtcNow) : Result<bool>.Success(false);
        if(window.IsFailure)return Result<(RefundCalculation,InvoiceRefundPreview)>.Failure(window.Error);
        var late=window.Value;
        var preview=new InvoiceRefundPreview(PreviewKey(request.Snapshot,request.Items,request.PaymentMethod),Money(c.Refund),Money(c.DebtReduction),Money(c.Total),Money(c.Discount),Money(c.Paid),Money(c.Total-c.Discount-c.Paid),c.FullCancellation,
            late,request.PaymentMethod=="Cash" && c.Refund>Math.Max(0,cash),Money(cash));
        return Result<(RefundCalculation,InvoiceRefundPreview)>.Success((c,preview));
    }
    public async Task<Result<InvoiceRefundPreview>> PreviewAsync(int id,int actorId,InvoiceRefundPreviewRequest request,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        if(!await Admin(actorId,ct))return Result<InvoiceRefundPreview>.Failure(Error.Forbidden);
        var i=await Load(id,ct);if(i is null)return Result<InvoiceRefundPreview>.Failure(Error.NotFound);
        var result=await Prepare(i,actorId,request,ct);if(result.IsFailure)return Result<InvoiceRefundPreview>.Failure(result.Error);
        await tx.CommitAsync(ct);return Result<InvoiceRefundPreview>.Success(result.Value.Preview);
    }
    public async Task<Result<RefundRecordDto>> RefundAsync(int id,int actorId,InvoiceRefundRequest request,string? ip,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);var committed=false;
        try
        {
            if(!await Admin(actorId,ct))return Result<RefundRecordDto>.Failure(Error.Forbidden);
            var hash=Payload(request);
            var existing=await db.InvoiceRefunds.Include(r=>r.Actor).SingleOrDefaultAsync(r=>r.RequestId==request.RequestId,ct);
            if(existing is not null)
            {
                if(existing.InvoiceId!=id || existing.ActorId!=actorId || existing.PayloadHash!=hash)return Result<RefundRecordDto>.Failure(Changed);
                await tx.CommitAsync(ct);committed=true;return Result<RefundRecordDto>.Success(Map(existing));
            }
            var i=await Load(id,ct);if(i is null)return Result<RefundRecordDto>.Failure(Error.NotFound);
            var prepared=await Prepare(i,actorId,new(request.Snapshot,request.Items,request.PaymentMethod),ct);
            if(prepared.IsFailure)return Result<RefundRecordDto>.Failure(prepared.Error);
            var(c,preview)=prepared.Value;
            if(preview.Preview!=request.ExpectedPreview)return Result<RefundRecordDto>.Failure(Changed);
            if(preview.RequiresLateApproval && !request.LateApproval || preview.RequiresReserve && !request.ReserveConfirmed)
                return Result<RefundRecordDto>.Failure(new("BIL_REFUND_APPROVAL","Cần xác nhận ngoại lệ quá7ngày hoặc xuất quỹ dự phòng theo phần xem trước."));
            var record=new InvoiceRefund { Invoice=i,Revision=i.RefundRevision+1,RequestId=request.RequestId,PayloadHash=hash,ActorId=actorId,CreatedAt=clock.UtcNow,
                RefundAmount=c.Refund,DebtReduction=c.DebtReduction,TotalBefore=i.TotalAmount,DiscountBefore=i.DiscountAmount,PaidBefore=i.PaidAmount,
                TotalAfter=c.Total,DiscountAfter=c.Discount,PaidAfter=c.Paid,ReasonCode=request.ReasonCode,ReasonNotes=request.ReasonNotes.Trim(),PaymentMethod=request.PaymentMethod,
                LateApproval=preview.RequiresLateApproval,ReserveConfirmed=preview.RequiresReserve,
                Items=request.Items.Select(x=>new InvoiceRefundItem{InvoiceItemId=x.InvoiceItemId,Quantity=x.Quantity}).ToList() };
            if(c.Refund>0)
                record.PaymentTransaction=new PaymentTransaction{Invoice=i,Amount=-c.Refund,CashierId=actorId,PaidAt=clock.UtcNow,RequestId=request.RequestId,PaymentMethod=request.PaymentMethod,
                    TransactionType="Refund",RefundReasonCode=record.ReasonCode,RefundReasonNotes=record.ReasonNotes};
            i.TotalAmount=c.Total;i.DiscountAmount=c.Discount;i.PaidAmount=c.Paid;i.RefundRevision++;
            i.Status=c.FullCancellation?InvoiceStatus.Refunded:InvoiceStatus.PartiallyRefunded;
            db.InvoiceRefunds.Add(record);
            await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog{UserId=actorId,EntityType="Invoice",EntityId=id,Action="MOD_BIL_REFUND_RECORDED",CreatedAt=clock.UtcNow,IpAddress=ip,
                Detail=JsonSerializer.Serialize(new{refundId=record.Id,revision=i.RefundRevision,paymentId=record.PaymentTransactionId,record.LateApproval,record.ReserveConfirmed})});
            await db.SaveChangesAsync(ct);
            record.Actor=await db.Users.SingleAsync(u=>u.UserId==actorId,ct);
            await tx.CommitAsync(ct);committed=true;return Result<RefundRecordDto>.Success(Map(record));
        }
        catch(Exception error)when(IsConflict(error)){return Result<RefundRecordDto>.Failure(Changed);}
        finally{if(!committed)db.ChangeTracker.Clear();}
    }
    public async Task<Result<InvoiceRefundVoucher>> PrintAsync(int id,int refundId,int actorId,string? ip,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);var committed=false;
        try
        {
            if(!await Admin(actorId,ct))return Result<InvoiceRefundVoucher>.Failure(Error.Forbidden);
            var r=await db.InvoiceRefunds.Include(x=>x.Actor).Include(x=>x.PaymentTransaction).Include(x=>x.Invoice).ThenInclude(x=>x.Visit).ThenInclude(x=>x.Patient)
                .SingleOrDefaultAsync(x=>x.Id==refundId && x.InvoiceId==id,ct);
            if(r is null)return Result<InvoiceRefundVoucher>.Failure(Error.NotFound);
            if(r.RefundAmount<=0 || r.PaymentTransaction is not {} payment || payment.Amount!=-r.RefundAmount || payment.TransactionType!="Refund" || payment.PrintCount==int.MaxValue)
                return Result<InvoiceRefundVoucher>.Failure(Error.Conflict);
            payment.PrintCount++;
            db.AuditLogs.Add(new AuditLog{UserId=actorId,EntityType="InvoiceRefund",EntityId=r.Id,Action="MOD_BIL_REFUND_PRINT_PREPARED",CreatedAt=clock.UtcNow,IpAddress=ip,
                Detail=JsonSerializer.Serialize(new{paymentId=payment.Id,printNumber=payment.PrintCount})});
            var settings=await db.SystemConfigs.Where(x=>x.Key=="Clinic:Address" || x.Key=="Clinic:Phone").ToDictionaryAsync(x=>x.Key,x=>x.Value,ct);
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);committed=true;
            return Result<InvoiceRefundVoucher>.Success(new(Map(r),r.Invoice.InvoiceCode,"Nha khoa H&G",settings.GetValueOrDefault("Clinic:Address"),settings.GetValueOrDefault("Clinic:Phone"),r.Invoice.Visit.Patient.FullName,payment.PrintCount));
        }
        catch(Exception error)when(IsConflict(error)){return Result<InvoiceRefundVoucher>.Failure(Changed);}
        finally{if(!committed)db.ChangeTracker.Clear();}
    }
    private static bool IsConflict(Exception error)
    {
        for(Exception? e=error;e is not null;e=e.InnerException)if(e is DbUpdateConcurrencyException || e is PostgresException{SqlState:"40001" or "40P01" or "23505"})return true;
        return false;
    }
}
