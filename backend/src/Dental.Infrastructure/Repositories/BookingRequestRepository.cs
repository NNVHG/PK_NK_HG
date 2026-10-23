using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_APP;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Dental.Infrastructure.Repositories;

public sealed class BookingRequestRepository(DentalDbContext db,VietnamClock clock):IBookingRequestRepository
{
    public static string Digest(BookingIntent intent,int? actor)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{intent,actor})));
    private Task<bool> Staff(int actor,string role,CancellationToken ct)=>role is RoleCodes.Admin or RoleCodes.Receptionist
        ?db.Users.AnyAsync(u=>u.UserId==actor&&u.IsActive&&u.Role.RoleCode==role,ct):Task.FromResult(false);
    public async Task<PublicBookingCatalog> CatalogAsync(CancellationToken ct)
    {
        var s=await ClinicScheduleReader.ReadAsync(db,ct);
        var dentists=await db.Users.AsNoTracking().Where(u=>u.IsActive&&u.Role.RoleCode==RoleCodes.Dentist).OrderBy(u=>u.FullName).ThenBy(u=>u.UserId).Select(u=>new PublicBookingDentist(u.UserId,u.FullName)).ToArrayAsync(ct);
        return new(clock.Today.ToString("yyyy-MM-dd"),s.Schedule.Slots().Select(t=>t.ToString("HH:mm")).ToArray(),s.Schedule.DurationMinutes,s.Revision,dentists);
    }
    public async Task<Result<bool>> ValidateAsync(BookingIntent i,int? actor,string? role,CancellationToken ct)
    {
        if(actor.HasValue&&!(role==RoleCodes.Patient&&await db.Users.AnyAsync(u=>u.UserId==actor&&u.IsActive&&u.Role.RoleCode==RoleCodes.Patient&&u.Phone==i.Phone,ct)))return Result<bool>.Failure(Error.Forbidden);
        if(i.PatientId.HasValue&&(!actor.HasValue||!await db.Patients.AnyAsync(p=>p.PatientId==i.PatientId&&p.IsActive&&p.UserId==actor&&p.FullName==i.FullName.Trim()&&p.DateOfBirth==i.DateOfBirth,ct)))return Result<bool>.Failure(Error.NotFound);
        var s=await ClinicScheduleReader.ReadAsync(db,ct);
        if(s.Revision!=i.ScheduleRevision||!s.Schedule.IsValidSlot(i.SlotTime)||i.AppointmentDate<clock.Today||i.AppointmentDate==clock.Today&&i.SlotTime<=TimeOnly.FromDateTime(clock.Now))return Result<bool>.Failure(new Error("APP_REQUEST_SCHEDULE","Khung hẹn không còn hợp lệ. Tải lại và chọn giờ trước khi gửi."));
        if(i.DentistId.HasValue&&!await db.Users.AnyAsync(u=>u.UserId==i.DentistId&&u.IsActive&&u.Role.RoleCode==RoleCodes.Dentist,ct))return Result<bool>.Failure(Error.Validation);
        return Result<bool>.Success(true);
    }
    public async Task<BookingReceipt?> ReplayAsync(SubmitBookingRequest request,int? actor,CancellationToken ct)
    {
        if(actor.HasValue&&!await db.Users.AnyAsync(u=>u.UserId==actor&&u.IsActive&&u.Role.RoleCode==RoleCodes.Patient,ct))return null;
        var digest=Digest(request.Intent,actor);
        return await db.BookingRequests.AsNoTracking().Where(x=>x.ClientRequestId==request.ClientRequestId&&x.IntentDigest==digest&&x.SubmittedByUserId==actor).Select(x=>new BookingReceipt(x.ClientRequestId,x.Status)).SingleOrDefaultAsync(ct);
    }
    public async Task<Result<BookingReceipt>> SubmitAsync(SubmitBookingRequest request,int? actor,string? role,string? ip,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);var committed=false;
        try
        {
            var replay=await ReplayAsync(request,actor,ct);if(replay is not null)return Result<BookingReceipt>.Success(replay);
            var valid=await ValidateAsync(request.Intent,actor,role,ct);if(valid.IsFailure)return Result<BookingReceipt>.Failure(valid.Error);
            var i=request.Intent;var schedule=await ClinicScheduleReader.ReadAsync(db,ct);var duration=schedule.Schedule.DurationMinutes;
            var appointments=new AppointmentRepository(db);
            if(await appointments.GetSlotBookingCountAsync(i.AppointmentDate,i.SlotTime,ct,durationMinutes:duration)>=AppointmentSlots.MaxBookingsPerSlot)return Result<BookingReceipt>.Failure(Error.AppointmentSlotFull);
            if(i.DentistId.HasValue&&await appointments.HasDentistBookingAsync(i.DentistId.Value,i.AppointmentDate,i.SlotTime,0,ct,duration))return Result<BookingReceipt>.Failure(new Error("APP_REQUEST_HELD","Nha sĩ đã có lịch hoặc yêu cầu giữ chỗ trong khung giờ này."));
            if(i.PatientId.HasValue&&(await appointments.HasActiveAppointmentOnDateAsync(i.PatientId.Value,i.AppointmentDate,ct)||await db.BookingRequests.AnyAsync(x=>x.Status=="Pending"&&x.VerifiedPatientId==i.PatientId&&x.AppointmentDate==i.AppointmentDate,ct)))return Result<BookingReceipt>.Failure(Error.AppointmentPatientHasActive);
            if(!i.PatientId.HasValue&&await db.BookingRequests.AnyAsync(x=>x.Status=="Pending"&&x.Phone==i.Phone&&x.FullName==i.FullName.Trim()&&x.DateOfBirth==i.DateOfBirth&&x.AppointmentDate==i.AppointmentDate,ct))return Result<BookingReceipt>.Failure(new Error("APP_REQUEST_DUPLICATE","Đã có yêu cầu cùng thông tin trong ngày này. Liên hệ phòng khám để đối chiếu."));
            // Do not infer ownership or return visits by matching guest names or a shared phone number.
            var returning=i.PatientId.HasValue&&await db.Visits.AnyAsync(v=>v.PatientId==i.PatientId&&v.Status==VisitStatuses.Completed,ct);
            var row=new BookingRequest{ClientRequestId=request.ClientRequestId,IntentDigest=Digest(i,actor),FullName=i.FullName.Trim(),Phone=i.Phone,DateOfBirth=i.DateOfBirth,Gender=i.Gender,Email=string.IsNullOrWhiteSpace(i.Email)?null:i.Email.Trim(),Notes=string.IsNullOrWhiteSpace(i.Notes)?null:i.Notes.Trim(),AppointmentDate=i.AppointmentDate,SlotTime=i.SlotTime,SlotDurationMinutes=duration,DentistId=i.DentistId,VerifiedPatientId=i.PatientId,SubmittedByUserId=actor,IsReturning=returning,CreatedAt=clock.UtcNow};
            db.BookingRequests.Add(row);await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog{UserId=actor,EntityType="BookingRequest",EntityId=row.Id,Action="MOD_APP_REQUEST_SUBMITTED",IpAddress=ip,CreatedAt=clock.UtcNow,Detail=JsonSerializer.Serialize(new{version=2,phoneVerified=false,verifiedProfile=i.PatientId.HasValue})});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);committed=true;
            return Result<BookingReceipt>.Success(new(row.ClientRequestId,row.Status));
        }
        catch(Exception e)when(IsConflict(e)){return Conflict<BookingReceipt>();}
        finally{if(!committed)db.ChangeTracker.Clear();}
    }
    public async Task<Result<PagedResult<BookingRequestRow>>> ListAsync(int actor,string role,int page,CancellationToken ct)
    {
        if(!await Staff(actor,role,ct))return Result<PagedResult<BookingRequestRow>>.Failure(Error.Forbidden);
        var q=db.BookingRequests.AsNoTracking().Where(x=>x.Status=="Pending");var count=await q.CountAsync(ct);
        var items=await q.OrderByDescending(x=>x.IsReturning).ThenBy(x=>x.CreatedAt).ThenBy(x=>x.Id).Skip((page-1)*20).Take(20)
            .Select(x=>new BookingRequestRow(x.Id,x.FullName,x.Phone,x.DateOfBirth,x.AppointmentDate,x.SlotTime,x.SlotDurationMinutes,x.DentistId,db.Users.Where(u=>u.UserId==x.DentistId).Select(u=>u.FullName).FirstOrDefault(),x.Notes,x.IsReturning,x.CreatedAt,x.Version)).ToListAsync(ct);
        return Result<PagedResult<BookingRequestRow>>.Success(PagedResult<BookingRequestRow>.Create(items,count,page,20));
    }
    public async Task<Result<bool>> RejectAsync(int id,int actor,string role,RejectBookingRequest request,string? ip,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);var committed=false;
        try
        {
            if(!await Staff(actor,role,ct))return Result<bool>.Failure(Error.Forbidden);
            var row=await db.BookingRequests.SingleOrDefaultAsync(x=>x.Id==id,ct);if(row is null)return Result<bool>.Failure(Error.NotFound);
            if(row.Status!="Pending"||row.Version!=request.ExpectedVersion)return Conflict<bool>();
            row.Status="Rejected";row.RejectionReason=request.Reason.Trim();row.ReviewedByUserId=actor;row.UpdatedAt=clock.UtcNow;row.Version++;
            db.AuditLogs.Add(new AuditLog{UserId=actor,EntityType="BookingRequest",EntityId=id,Action="MOD_APP_REQUEST_REJECTED",IpAddress=ip,CreatedAt=clock.UtcNow,Detail=JsonSerializer.Serialize(new{version=row.Version,reasonProvided=true})});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);committed=true;return Result<bool>.Success(true);
        }
        catch(Exception e)when(IsConflict(e)){return Conflict<bool>();}
        finally{if(!committed)db.ChangeTracker.Clear();}
    }
    private static Result<T> Conflict<T>()=>Result<T>.Failure(new Error("APP_REQUEST_CONFLICT","Yêu cầu hoặc chỗ hẹn vừa thay đổi. Tải lại để đối chiếu."));
    private static bool IsConflict(Exception e){for(Exception? x=e;x is not null;x=x.InnerException)if(x is DbUpdateConcurrencyException||x is PostgresException{SqlState:"40001" or "40P01" or "23505"})return true;return false;}
}
