using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Dental.Infrastructure.Repositories;

public sealed class CounterPatientPhoneRepository(DentalDbContext db, VietnamClock clock) : ICounterPatientPhoneRepository
{
    private static bool OwnerEligible(User? u) => u is { IsActive: true, RequiresPhoneActivation: false } &&
        u.Role.RoleCode == RoleCodes.Patient && u.TokenVersion < int.MaxValue;
    public async Task<Result<CounterPhoneSuggestion?>> GetOwnAsync(int userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.Role).SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (!OwnerEligible(u)) return Result<CounterPhoneSuggestion?>.Failure(Error.Forbidden);
        var suggestion = await db.PatientPhoneChangeRequests.AsNoTracking()
            .Where(x => x.UserId == userId && x.CurrentPhone == u!.Phone && x.TokenVersion == u.TokenVersion && x.CompletedAtUtc == null)
            .Select(x => new CounterPhoneSuggestion(x.Id, x.AppointmentId, x.NewPhone, x.CreatedAtUtc)).SingleOrDefaultAsync(ct);
        return Result<CounterPhoneSuggestion?>.Success(suggestion);
    }
    public async Task<Result<CounterPhoneReceipt>> CreateAsync(int actorId, int appointmentId, CounterPhoneRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            var actor = await db.Users.AsNoTracking().Include(x => x.Role).SingleOrDefaultAsync(x => x.UserId == actorId, ct);
            if (actor is not { IsActive: true, RequiresPhoneActivation: false } || actor.Role.RoleCode is not (RoleCodes.Admin or RoleCodes.Receptionist))
                return Result<CounterPhoneReceipt>.Failure(Error.Forbidden);
            var appointment = await db.Appointments.Include(x => x.Patient).SingleOrDefaultAsync(x => x.AppointmentId == appointmentId, ct);
            if (appointment is null) return Result<CounterPhoneReceipt>.Failure(Error.NotFound);
            if (appointment.Status != AppointmentStatuses.Scheduled || appointment.AppointmentDate < clock.Today || !appointment.Patient.IsActive || !appointment.Patient.UserId.HasValue)
                return Result<CounterPhoneReceipt>.Failure(Error.Conflict);
            var u = await db.Users.AsNoTracking().Include(x => x.Role).SingleOrDefaultAsync(x => x.UserId == appointment.Patient.UserId, ct);
            if (!OwnerEligible(u)) return Result<CounterPhoneReceipt>.Failure(Error.Conflict);
            if (request.NewPhone == u!.Phone || await db.Users.AnyAsync(x => x.Phone == request.NewPhone, ct))
                return Result<CounterPhoneReceipt>.Failure(PatientPhoneChangeService.NumberUnavailable);
            if (await db.PatientPhoneChangeRequests.AnyAsync(x => x.UserId == u.UserId && x.TokenVersion == u.TokenVersion && x.CompletedAtUtc == null, ct))
                return Result<CounterPhoneReceipt>.Failure(new Error("PAT_PHONE_PENDING", "Tài khoản đã có yêu cầu hiện hành. Chủ tài khoản đăng nhập để kiểm tra; nhân viên không ghi đè yêu cầu."));
            var row = new PatientPhoneChangeRequest { Id = Guid.NewGuid(), UserId = u.UserId, AppointmentId = appointmentId,
                CreatedByUserId = actorId, CurrentPhone = u.Phone, NewPhone = request.NewPhone, TokenVersion = u.TokenVersion, CreatedAtUtc = clock.UtcNow };
            db.PatientPhoneChangeRequests.Add(row);
            db.AuditLogs.Add(new AuditLog { UserId = actorId, EntityType = "PatientPhoneChangeRequest", Action = "MOD_AUTH_COUNTER_PHONE_REQUESTED",
                CreatedAt = clock.UtcNow, IpAddress = ip, Detail = JsonSerializer.Serialize(new { decision = "DL-335", requestId = row.Id, ownerUserId = u.UserId, appointmentId, requiresOwnerConfirmation = true }) });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct); committed = true;
            return Result<CounterPhoneReceipt>.Success(new(row.Id, true));
        }
        catch (Exception ex) when (Conflict(ex)) { return Result<CounterPhoneReceipt>.Failure(Error.Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static bool Conflict(Exception e)
    {
        for (Exception? x = e; x is not null; x = x.InnerException)
            if (x is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
