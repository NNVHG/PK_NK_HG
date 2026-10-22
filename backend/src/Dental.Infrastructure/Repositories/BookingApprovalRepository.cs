using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_APP;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Services;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class BookingApprovalRepository(DentalDbContext db, VietnamClock clock, IPasswordHasher passwords) : IBookingApprovalRepository
{
    private Task<bool> Staff(int id, string role, CancellationToken ct) => role is RoleCodes.Admin or RoleCodes.Receptionist
        ? db.Users.AnyAsync(u => u.UserId == id && u.IsActive && u.Role.RoleCode == role, ct) : Task.FromResult(false);

    private async Task<BookingApprovalContext> Context(BookingRequest row, CancellationToken ct)
    {
        var account = row.SubmittedByUserId.HasValue
            ? await db.Users.AsNoTracking().Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == row.SubmittedByUserId && u.Role.RoleCode == RoleCodes.Patient, ct)
            : null;
        var patients = await db.Patients.AsNoTracking().Where(p => p.Phone == row.Phone &&
            (p.FullName == row.FullName || p.DateOfBirth == row.DateOfBirth || p.PatientId == row.VerifiedPatientId))
            .OrderBy(p => p.PatientId).Take(21).ToArrayAsync(ct);
        var counter = !row.VerifiedPatientId.HasValue && patients.Length > 0;
        string? blocked = null;
        if (row.Status != "Pending") blocked = "Yêu cầu không còn chờ xác nhận.";
        else if (row.AppointmentDate < clock.Today || row.AppointmentDate == clock.Today && row.SlotTime <= TimeOnly.FromDateTime(clock.Now))
            blocked = "Giờ hẹn đã qua. Từ chối yêu cầu và đối chiếu lịch mới với khách.";
        else if (row.SubmittedByUserId.HasValue && (account is null || !account.IsActive))
            blocked = "Tài khoản gửi yêu cầu đã thay đổi. Cần đối chiếu lại.";
        else if (patients.Length > 20) blocked = "Có quá nhiều hồ sơ tương đồng. Đối soát hồ sơ tại quầy trước khi xử lý.";
        var dentist = row.DentistId.HasValue ? await db.Users.AsNoTracking().Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == row.DentistId, ct) : null;
        if (row.DentistId.HasValue && (dentist is null || !dentist.IsActive || dentist.Role.RoleCode != RoleCodes.Dentist))
            blocked = "Nha sĩ giữ chỗ không còn hoạt động. Đối chiếu lại yêu cầu với khách.";
        if (row.VerifiedPatientId.HasValue && !patients.Any(p => p.PatientId == row.VerifiedPatientId && p.IsActive && p.UserId == row.SubmittedByUserId))
            blocked = "Hồ sơ đã xác minh không còn thuộc tài khoản gửi yêu cầu hoặc đã ngừng hoạt động.";
        // The fingerprint detects a changed review, not a permission token. Authorization is repeated in the transaction.
        var stamp = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            row.Id, row.Version, row.Status, row.FullName, row.Phone, row.DateOfBirth, row.Gender, row.Email, row.Notes,
            row.AppointmentDate, row.SlotTime, row.SlotDurationMinutes, row.DentistId, row.VerifiedPatientId, row.SubmittedByUserId,
            account = account is null ? null : new { account.UserId, account.FullName, account.Phone, account.RoleId, account.IsActive, account.UpdatedAt, account.TokenVersion, account.RequiresPhoneActivation },
            patients = patients.Select(p => new { p.PatientId, p.FullName, p.DateOfBirth, p.Phone, p.UserId, p.IsActive, p.UpdatedAt, p.CitizenIdVersion }),
            dentist = dentist is null ? null : new { dentist.UserId, dentist.IsActive, dentist.RoleId, dentist.UpdatedAt }
        })));
        BookingApprovalResult? result = null;
        if (row.ApprovedAppointmentId.HasValue)
        {
            var a = await db.Appointments.AsNoTracking().SingleAsync(a => a.AppointmentId == row.ApprovedAppointmentId, ct);
            result = new(a.AppointmentId, a.PatientId, false, false);
        }
        return new(row.Id, row.Version, row.Status, row.FullName, row.Phone, row.DateOfBirth, row.Gender,
            row.AppointmentDate, row.SlotTime, row.SlotDurationMinutes, dentist?.FullName, row.VerifiedPatientId,
            account is null ? null : new(account.UserId, account.FullName),
            patients.Take(20).Select(p => new BookingApprovalPatient(p.PatientId, p.FullName, p.DateOfBirth,
                p.IsActive && (!row.VerifiedPatientId.HasValue || p.UserId == account?.UserId) &&
                (!row.VerifiedPatientId.HasValue || p.PatientId == row.VerifiedPatientId))).ToArray(),
            counter, blocked, stamp, result);
    }
    public async Task<Result<BookingApprovalContext>> ReadAsync(int id, int actor, string role, CancellationToken ct)
    {
        if (!await Staff(actor, role, ct)) return Result<BookingApprovalContext>.Failure(Error.Forbidden);
        var row = await db.BookingRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? Result<BookingApprovalContext>.Failure(Error.NotFound)
            : Result<BookingApprovalContext>.Success(await Context(row, ct));
    }
    public async Task<Result<BookingApprovalResult>> ApproveAsync(int id, int actor, string role, ApproveBookingRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (!await Staff(actor, role, ct)) return Result<BookingApprovalResult>.Failure(Error.Forbidden);
            var row = await db.BookingRequests.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Result<BookingApprovalResult>.Failure(Error.NotFound);
            var context = await Context(row, ct);
            if (context.BlockReason is not null || row.Version != request.ExpectedVersion || context.ReviewStamp != request.ReviewStamp)
                return Conflict();
            if (context.RequiresCounterConfirmation && !request.ConfirmedAtCounter)
                return Result<BookingApprovalResult>.Failure(new Error("APP_APPROVAL_COUNTER", "Phải đối soát và xác nhận tại quầy trước khi gắn hồ sơ. Yêu cầu vẫn Pending."));
            if (row.VerifiedPatientId.HasValue && request.PatientId != row.VerifiedPatientId) return Conflict();
            Patient patient;
            if (request.PatientId.HasValue)
            {
                if (!context.Patients.Any(p => p.PatientId == request.PatientId && p.CanSelect)) return Conflict();
                patient = await db.Patients.SingleAsync(p => p.PatientId == request.PatientId, ct);
                if (await db.Appointments.AnyAsync(a => a.PatientId == patient.PatientId && a.AppointmentDate == row.AppointmentDate && a.Status == AppointmentStatuses.Scheduled, ct)
                    || await db.BookingRequests.AnyAsync(x => x.Id != row.Id && x.Status == "Pending" && x.VerifiedPatientId == patient.PatientId && x.AppointmentDate == row.AppointmentDate, ct))
                    return Result<BookingApprovalResult>.Failure(Error.AppointmentPatientHasActive);
            }
            else
            {
                if (!request.ConfirmedNewPatient) return Conflict();
                patient = new Patient { FullName = row.FullName, Phone = row.Phone, DateOfBirth = row.DateOfBirth,
                    Gender = row.Gender, Email = row.Email, CreatedAt = clock.UtcNow };
                db.Patients.Add(patient);
            }
            // Replacing the same held interval adds no capacity and preserves the original schedule duration (DL316/327).
            var appointments = new AppointmentRepository(db);
            if (await appointments.GetSlotBookingCountAsync(row.AppointmentDate, row.SlotTime, ct, durationMinutes: row.SlotDurationMinutes) > AppointmentSlots.MaxBookingsPerSlot)
                return Result<BookingApprovalResult>.Failure(Error.AppointmentSlotFull);
            if (row.DentistId.HasValue)
            {
                var otherHolds = await BookingRequestReservations.Nearby(db, row.AppointmentDate).Where(x => x.Id != row.Id && x.DentistId == row.DentistId).ToArrayAsync(ct);
                // An explicitly acknowledged emergency overlap does not remove or invalidate the original hold (DL234).
                var first = DateOnly.FromDayNumber(Math.Max(DateOnly.MinValue.DayNumber, row.AppointmentDate.DayNumber - 1));
                var last = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, row.AppointmentDate.DayNumber + 1));
                var otherBookings = await db.Appointments.AsNoTracking().Where(a => a.DentistId == row.DentistId && a.Status == AppointmentStatuses.Scheduled && !a.IsEmergency
                    && a.AppointmentDate >= first && a.AppointmentDate <= last).ToArrayAsync(ct);
                if (otherHolds.Any(x => BookingOccupancy.Overlaps(BookingRequestReservations.Period(x), row.AppointmentDate, row.SlotTime, row.SlotDurationMinutes))
                    || otherBookings.Any(a => BookingOccupancy.Overlaps(a, row.AppointmentDate, row.SlotTime, row.SlotDurationMinutes))) return Conflict();
            }
            var appointment = new Appointment { Patient = patient, AppointmentDate = row.AppointmentDate, SlotTime = row.SlotTime,
                SlotDurationMinutes = row.SlotDurationMinutes, DentistId = row.DentistId, Notes = row.Notes,
                CreatedByUserId = actor, CreatedAt = clock.UtcNow };
            db.Appointments.Add(appointment);
            row.Status = "Approved"; row.Version++; row.ReviewedByUserId = actor; row.UpdatedAt = clock.UtcNow; row.CreatedAccount = false;
            await db.SaveChangesAsync(ct);
            row.ApprovedAppointmentId = appointment.AppointmentId;
            db.AuditLogs.Add(new AuditLog { UserId = actor, EntityType = "BookingRequest", EntityId = row.Id,
                Action = "MOD_APP_REQUEST_APPROVED", IpAddress = ip, CreatedAt = clock.UtcNow,
                Detail = JsonSerializer.Serialize(new { appointmentId = appointment.AppointmentId, patientId = patient.PatientId,
                    accountCreated = false, accountDeferredToCheckIn = true, confirmedAtCounter = request.ConfirmedAtCounter,
                    version = row.Version, decision = "DL-329" }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<BookingApprovalResult>.Success(new(appointment.AppointmentId, patient.PatientId, false, false));
        }
        catch (Exception e) when (IsConflict(e)) { return Conflict(); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static Result<BookingApprovalResult> Conflict() => Result<BookingApprovalResult>.Failure(new Error("APP_APPROVAL_CONFLICT", "Yêu cầu hoặc hồ sơ vừa thay đổi. Tải lại để đối soát trước khi xác nhận."));
    private static bool IsConflict(Exception e)
    {
        for (Exception? x = e; x is not null; x = x.InnerException)
            if (x is DbUpdateConcurrencyException || x is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
