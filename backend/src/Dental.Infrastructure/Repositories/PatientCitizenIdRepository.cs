using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_PAT;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;
public sealed class PatientCitizenIdRepository(DentalDbContext db, IKeyedIdentityHasher identityHasher) : IPatientCitizenIdRepository
{
    public async Task<Result<CitizenIdStatus>> ReadAsync(int patientId, int actorId, string role, CancellationToken ct)
    {
        if (!await ActiveActor(actorId, role, ct)) return Result<CitizenIdStatus>.Failure(Error.Forbidden);
        var p = await db.Patients.AsNoTracking().Where(p => p.PatientId == patientId && (role != RoleCodes.Patient || p.UserId == actorId))
            .Select(p => new CitizenIdStatus(p.PatientId, p.CitizenIdMasked, p.CitizenIdVersion)).SingleOrDefaultAsync(ct);
        return p is null ? Result<CitizenIdStatus>.Failure(Error.NotFound) : Result<CitizenIdStatus>.Success(p);
    }
    public async Task<Result<CitizenIdStatus>> SaveAsync(int patientId, int actorId, string role, CitizenIdRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (role is not (RoleCodes.Admin or RoleCodes.Receptionist) || !await ActiveActor(actorId, role, ct))
                return Result<CitizenIdStatus>.Failure(Error.Forbidden);
            var p = await db.Patients.SingleOrDefaultAsync(p => p.PatientId == patientId, ct);
            if (p is null) return Result<CitizenIdStatus>.Failure(Error.NotFound);
            if (p.CitizenIdVersion != request.ExpectedVersion) return Conflict();
            var hash = identityHasher.Hash(request.CitizenId);
            var legacyHash = CitizenIdIdentity.Hash(request.CitizenId);
            if (await db.Patients.AnyAsync(x => x.PatientId != patientId &&
                (x.CitizenIdHash == hash || x.CitizenIdHash == legacyHash), ct))
                return Result<CitizenIdStatus>.Failure(new Error("PAT_ID_DUPLICATE", "CCCD đã thuộc hồ sơ khác. Kiểm tra hồ sơ trước khi lưu."));
            p.CitizenIdHash = hash; p.CitizenIdMasked = identityHasher.Mask(request.CitizenId);
            p.DocumentType = IdentityDocumentType.CitizenId; p.IssuingCountryCode = null;
            p.HashScheme = IdentityHashScheme.HmacCanonical;
            p.IdentityKeyVersion = IdentityDocumentConstants.CurrentHmacKeyVersion; p.CitizenIdVersion++;
            p.UpdatedAt = DateTime.UtcNow;
            db.AuditLogs.Add(new AuditLog { UserId = actorId, EntityType = "Patient", EntityId = patientId,
                Action = "MOD_PAT_CITIZEN_ID_UPDATED", CreatedAt = DateTime.UtcNow, IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { version = p.CitizenIdVersion }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<CitizenIdStatus>.Success(new(patientId, p.CitizenIdMasked, p.CitizenIdVersion));
        }
        catch (Exception e) when (ConflictException(e)) { return Conflict(); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private Task<bool> ActiveActor(int id, string role, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.UserId == id && u.IsActive && u.Role.RoleCode == role, ct);
    private static Result<CitizenIdStatus> Conflict() => Result<CitizenIdStatus>.Failure(new Error("PAT_ID_CONFLICT", "Hồ sơ vừa thay đổi. Tải lại để đối chiếu."));
    private static bool ConflictException(Exception e)
    {
        for (Exception? x = e; x is not null; x = x.InnerException)
            if (x is DbUpdateConcurrencyException || x is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
