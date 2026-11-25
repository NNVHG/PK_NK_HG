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

public sealed class BookingApprovalRepository(DentalDbContext db, VietnamClock clock, IPasswordHasher? passwords = null) : IBookingApprovalRepository
{
    private Task<bool> Staff(int id, string role, CancellationToken ct) => role is RoleCodes.Admin or RoleCodes.Receptionist
        ? db.Users.AnyAsync(u => u.UserId == id && u.IsActive && u.Role.RoleCode == role, ct) : Task.FromResult(false);

    private async Task<BookingApprovalContext> Context(Appointment row, CancellationToken ct)
    {
        var fullName = row.GuestFullName ?? row.Patient?.FullName ?? "";
        var phone = row.GuestPhone ?? row.Patient?.Phone ?? "";
        var dob = row.GuestDateOfBirth ?? row.Patient?.DateOfBirth ?? default;
        var gender = row.GuestGender ?? row.Patient?.Gender ?? "";
        var email = row.GuestEmail ?? row.Patient?.Email;
        var verifiedPatientId = row.Status == "Pending" ? row.PatientId : null;
        var mappedStatus = row.ToBookingRequestStatus();

        var account = row.SubmittedByUserId.HasValue
            ? await db.Users.AsNoTracking().Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == row.SubmittedByUserId && u.Role.RoleCode == RoleCodes.Patient, ct)
            : null;
        var patients = await db.Patients.AsNoTracking().Where(p => p.Phone == phone &&
            (p.FullName == fullName || p.DateOfBirth == dob || p.PatientId == row.PatientId))
            .OrderBy(p => p.PatientId).Take(21).ToArrayAsync(ct);
        var counter = !verifiedPatientId.HasValue && patients.Length > 0;
        string? blocked = null;
        if (row.Status != "Pending") blocked = "Yêu cầu không còn chờ xác nhận.";
        else if (row.AppointmentDate < clock.Today || row.AppointmentDate == clock.Today && row.SlotTime <= TimeOnly.FromDateTime(clock.Now))
            blocked = "Giờ hẹn đã qua. Từ chối yêu cầu và đối chiếu lịch mới với khách.";
        else if (row.SubmittedByUserId.HasValue && (account is null || !account.IsActive))
            blocked = "Tài khoản gửi yêu cầu đã thay đổi. Cần đối chiếu lại.";
        else if (patients.Length > 20) blocked = "Có quá nhiều hồ sơ tương đồng. Đối soát hồ sơ tại quầy trước khi xử lý.";
        if (verifiedPatientId.HasValue && !patients.Any(p => p.PatientId == verifiedPatientId && p.IsActive && p.UserId == row.SubmittedByUserId))
            blocked = "Hồ sơ đã xác minh không còn thuộc tài khoản gửi yêu cầu hoặc đã ngừng hoạt động.";

        // The fingerprint detects a changed review, not a permission token. Authorization is repeated in the transaction.
        var stamp = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Id = row.AppointmentId, row.Version, Status = mappedStatus, FullName = fullName, Phone = phone,
            DateOfBirth = dob, Gender = gender, Email = email, row.Notes,
            row.AppointmentDate, row.SlotTime, row.SlotDurationMinutes, VerifiedPatientId = verifiedPatientId, row.SubmittedByUserId,
            account = account is null ? null : new { account.UserId, account.FullName, account.Phone, account.RoleId, account.IsActive, account.UpdatedAt, account.TokenVersion, account.RequiresPhoneActivation },
            patients = patients.Select(p => new { p.PatientId, p.FullName, p.DateOfBirth, p.Phone, p.UserId, p.IsActive, p.UpdatedAt, p.CitizenIdVersion })
        })));

        BookingApprovalResult? result = null;
        if (mappedStatus == "Approved" && row.PatientId.HasValue)
            result = new(row.AppointmentId, row.PatientId.Value, false, false);

        return new(row.AppointmentId, row.Version, mappedStatus, fullName, phone, dob, gender,
            row.AppointmentDate, row.SlotTime, row.SlotDurationMinutes, verifiedPatientId,
            account is null ? null : new(account.UserId, account.FullName),
            patients.Take(20).Select(p => new BookingApprovalPatient(
                p.PatientId,
                p.FullName,
                p.DateOfBirth,
                BookingApprovalPolicy.IsCandidateSelectable(
                    dob,
                    p.DateOfBirth,
                    p.IsActive,
                    p.UserId,
                    verifiedPatientId,
                    account?.UserId,
                    p.PatientId,
                    clock.Today))).ToArray(),
            counter, blocked, stamp, result, row.Notes,
            PatientAccountAge.IsUnderMinimumAge(dob, clock.Today));
    }

    public async Task<Result<BookingApprovalContext>> ReadAsync(int id, int actor, string role, CancellationToken ct)
    {
        if (!await Staff(actor, role, ct)) return Result<BookingApprovalContext>.Failure(Error.Forbidden);
        var row = await db.Appointments.AsNoTracking().Include(x => x.Patient).SingleOrDefaultAsync(x => x.AppointmentId == id && x.Source == "Online", ct);
        return row is null ? Result<BookingApprovalContext>.Failure(Error.NotFound)
            : Result<BookingApprovalContext>.Success(await Context(row, ct));
    }

    public async Task<Result<BookingApprovalResult>> ApproveAsync(int id, int actor, string role, ApproveBookingRequest request, string? ip, CancellationToken ct)
    {
        _ = passwords;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (!await Staff(actor, role, ct)) return Result<BookingApprovalResult>.Failure(Error.Forbidden);
            var row = await db.Appointments.Include(x => x.Patient).SingleOrDefaultAsync(x => x.AppointmentId == id && x.Source == "Online", ct);
            if (row is null) return Result<BookingApprovalResult>.Failure(Error.NotFound);
            var context = await Context(row, ct);
            if (context.BlockReason is not null || row.Version != request.ExpectedVersion || context.ReviewStamp != request.ReviewStamp)
                return Conflict();
            if (context.RequiresCounterConfirmation && !request.ConfirmedAtCounter)
                return Result<BookingApprovalResult>.Failure(new Error("APP_APPROVAL_COUNTER", "Phải đối soát và xác nhận tại quầy trước khi gắn hồ sơ. Yêu cầu vẫn Pending."));
            if (context.VerifiedPatientId.HasValue && request.PatientId != context.VerifiedPatientId) return Conflict();

            Patient patient;
            if (request.PatientId.HasValue)
            {
                if (!context.Patients.Any(p => p.PatientId == request.PatientId && p.CanSelect)) return Conflict();
                patient = await db.Patients.SingleAsync(p => p.PatientId == request.PatientId, ct);
                if (await db.Appointments.AnyAsync(a => a.AppointmentId != row.AppointmentId && a.PatientId == patient.PatientId &&
                    a.AppointmentDate == row.AppointmentDate && (a.Status == AppointmentStatuses.Scheduled || a.Status == "Pending"), ct))
                    return Result<BookingApprovalResult>.Failure(Error.AppointmentPatientHasActive);
            }
            else
            {
                if (!request.ConfirmedNewPatient) return Conflict();
                patient = new Patient
                {
                    FullName = context.FullName, Phone = context.Phone, DateOfBirth = context.DateOfBirth,
                    Gender = context.Gender, Email = row.GuestEmail, CreatedAt = clock.UtcNow
                };
                db.Patients.Add(patient);
            }

            // Replacing the same held interval adds no capacity and preserves the original schedule duration (DL316/327).
            var appointments = new AppointmentRepository(db);
            if (await appointments.GetSlotBookingCountAsync(row.AppointmentDate, row.SlotTime, ct, excludedId: row.AppointmentId, durationMinutes: row.SlotDurationMinutes) >= AppointmentSlots.MaxBookingsPerSlot)
                return Result<BookingApprovalResult>.Failure(Error.AppointmentSlotFull);

            row.Patient = patient;
            row.Status = AppointmentStatuses.Scheduled;
            row.CreatedByUserId = actor;
            row.Version++;
            row.ReviewedByUserId = actor;
            row.UpdatedAt = clock.UtcNow;
            row.CreatedAccount = false;
            await db.SaveChangesAsync(ct);

            db.AuditLogs.Add(new AuditLog
            {
                UserId = actor, EntityType = "BookingRequest", EntityId = row.AppointmentId,
                Action = "MOD_APP_REQUEST_APPROVED", IpAddress = ip, CreatedAt = clock.UtcNow,
                Detail = JsonSerializer.Serialize(new
                {
                    appointmentId = row.AppointmentId,
                    patientId = patient.PatientId,
                    accountCreated = false,
                    accountDeferredToCheckIn = true,
                    confirmedAtCounter = request.ConfirmedAtCounter,
                    version = row.Version,
                    decision = "DL-329"
                })
            });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<BookingApprovalResult>.Success(new(row.AppointmentId, patient.PatientId, false, false));
        }
        catch (Exception e) when (IsConflict(e)) { return Conflict(); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }

    private static Result<BookingApprovalResult> Conflict()
        => Result<BookingApprovalResult>.Failure(new Error("APP_APPROVAL_CONFLICT", "Yêu cầu hoặc hồ sơ vừa thay đổi. Tải lại để đối soát trước khi xác nhận."));

    private static bool IsConflict(Exception e)
    {
        for (Exception? x = e; x is not null; x = x.InnerException)
            if (x is DbUpdateConcurrencyException || x is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
