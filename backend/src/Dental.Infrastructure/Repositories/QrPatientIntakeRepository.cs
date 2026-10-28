using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_PAT;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Errors;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;
public sealed class QrPatientIntakeRepository(DentalDbContext db, VietnamClock clock, IKeyedIdentityHasher identityHasher) : IQrPatientIntakeRepository
{
    private Task<bool> Allowed(int actorId, string role, CancellationToken ct) =>
        role is RoleCodes.Admin or RoleCodes.Receptionist
            ? db.Users.AnyAsync(u => u.UserId == actorId && u.IsActive && u.Role.RoleCode == role, ct)
            : Task.FromResult(false);
    public async Task<Result<QrIdentityLookupResult>> LookupAsync(int actorId, string role, QrIdentityLookupRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await Allowed(actorId, role, ct)) return Result<QrIdentityLookupResult>.Failure(Error.Forbidden);
        var hash = identityHasher.Hash(request.CitizenId);
        var legacyHash = CitizenIdIdentity.Hash(request.CitizenId);
        var patient = await db.Patients.AsNoTracking().SingleOrDefaultAsync(p => p.CitizenIdHash == hash || p.CitizenIdHash == legacyHash, ct);
        var existing = patient is null ? null : new QrExistingPatient(patient.PatientId, patient.PatientCode, patient.FullName, patient.CitizenIdMasked, patient.IsActive);
        await tx.CommitAsync(ct);
        return Result<QrIdentityLookupResult>.Success(new(existing));
    }
    public async Task<Result<PatientResponse>> CreateAsync(int actorId, string role, QrPatientIntakeRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (!await Allowed(actorId, role, ct)) return Result<PatientResponse>.Failure(Error.Forbidden);
            var hash = identityHasher.Hash(request.CitizenId);
            var legacyHash = CitizenIdIdentity.Hash(request.CitizenId);
            // Includes archived profiles: a shared phone confirmation never overrides identity uniqueness.
            if (await db.Patients.AnyAsync(p => p.CitizenIdHash == hash || p.CitizenIdHash == legacyHash, ct))
                return Result<PatientResponse>.Failure(new("PAT_QR_EXISTS", "CCCD đã có hồ sơ. Quét lại để mở hồ sơ cũ; không tạo mới."));
            var input = request.Patient;
            if (!input.ConfirmNotDuplicate)
            {
                var shared = await db.Patients.AsNoTracking().Where(p => p.Phone == input.Phone).OrderBy(p => p.PatientId).Take(20).ToListAsync(ct);
                if (shared.Count > 0) return Result<PatientResponse>.Failure(new PatientPossibleDuplicateError(shared.Select(p => new PatientDuplicateInfo(p.PatientCode, p.FullName, p.DateOfBirth, p.Phone)).ToArray()));
            }
            var patient = new Patient { FullName = input.FullName.Trim(), DateOfBirth = input.DateOfBirth, Gender = input.Gender,
                Phone = input.Phone, Email = input.Email?.Trim(), Address = input.Address?.Trim(), IsActive = true, CreatedAt = clock.UtcNow,
                CitizenIdHash = hash, CitizenIdMasked = identityHasher.Mask(request.CitizenId), CitizenIdVersion = 1,
                DocumentType = IdentityDocumentType.CitizenId, HashScheme = IdentityHashScheme.HmacCanonical,
                IdentityKeyVersion = IdentityDocumentConstants.CurrentHmacKeyVersion };
            db.Patients.Add(patient);
            await db.SaveChangesAsync(ct);
            // No raw QR, hash, identity fragments or demographic data in audit metadata.
            db.AuditLogs.Add(new AuditLog { UserId = actorId, EntityId = patient.PatientId, EntityType = "Patient",
                Action = "MOD_PAT_QR_INTAKE_CREATED", IpAddress = ip, CreatedAt = clock.UtcNow,
                Detail = JsonSerializer.Serialize(new { identityVersion = 1, sharedPhoneConfirmed = input.ConfirmNotDuplicate }) });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct); committed = true;
            return Result<PatientResponse>.Success(new(patient.PatientId, patient.PatientCode, patient.FullName, patient.DateOfBirth, patient.Gender,
                patient.Phone, patient.Email, patient.Address, patient.IsActive));
        }
        catch (Exception e) when (ConflictException(e))
        { return Result<PatientResponse>.Failure(new("PAT_QR_CONFLICT", "Dữ liệu vừa thay đổi. Quét lại để đối soát trước khi tiếp tục.")); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static bool ConflictException(Exception e)
    {
        for (Exception? current = e; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException || current is PostgresException { SqlState: "23505" or "40001" or "40P01" }) return true;
        return false;
    }
}
