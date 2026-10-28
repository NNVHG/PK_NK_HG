using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_PAT;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class PatientGuardianRepository(DentalDbContext db, IPasswordHasher passwordHasher, IKeyedIdentityHasher identityHasher) : IPatientGuardianRepository
{
    public async Task<PatientGuardian?> GetActiveByWardPatientIdAsync(int wardPatientId, CancellationToken ct = default)
    {
        return await db.PatientGuardians
            .Include(g => g.GuardianPatient)
            .Include(g => g.WardPatient)
            .Include(g => g.AssignedByUser)
            .FirstOrDefaultAsync(g => g.WardPatientId == wardPatientId && g.IsActive, ct);
    }

    public async Task<IReadOnlyList<PatientGuardian>> GetActiveByGuardianPatientIdAsync(int guardianPatientId, CancellationToken ct = default)
    {
        return await db.PatientGuardians
            .Include(g => g.WardPatient)
            .Where(g => g.GuardianPatientId == guardianPatientId && g.IsActive)
            .OrderBy(g => g.WardPatient!.FullName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PatientGuardian>> GetActiveByGuardianUserIdAsync(int guardianUserId, CancellationToken ct = default)
    {
        return await db.PatientGuardians
            .Include(g => g.WardPatient)
            .Include(g => g.GuardianPatient)
            .Where(g => g.GuardianPatient!.UserId == guardianUserId && g.IsActive)
            .OrderBy(g => g.WardPatient!.FullName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PatientGuardian>> GetHistoryByWardPatientIdAsync(int wardPatientId, CancellationToken ct = default)
    {
        return await db.PatientGuardians
            .Include(g => g.GuardianPatient)
            .Include(g => g.AssignedByUser)
            .Include(g => g.RevokedByUser)
            .Where(g => g.WardPatientId == wardPatientId)
            .OrderByDescending(g => g.AssignedAt)
            .ToListAsync(ct);
    }

    public async Task<Result<PatientGuardianResponse>> AssignGuardianAsync(
        AssignPatientGuardianRequest request, int actorUserId, string? ipAddress, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var guardian = await db.Patients.FirstOrDefaultAsync(p => p.PatientId == request.GuardianPatientId, ct);
            if (guardian is null || !guardian.IsActive)
                return Result<PatientGuardianResponse>.Failure(new Error("PAT_GUARDIAN_NOT_FOUND", "Hồ sơ người giám hộ không tồn tại hoặc đã ngừng hoạt động."));

            var ward = await db.Patients.FirstOrDefaultAsync(p => p.PatientId == request.WardPatientId, ct);
            if (ward is null || !ward.IsActive)
                return Result<PatientGuardianResponse>.Failure(new Error("PAT_WARD_NOT_FOUND", "Hồ sơ người được giám hộ không tồn tại hoặc đã ngừng hoạt động."));

            var existingActive = await db.PatientGuardians
                .FirstOrDefaultAsync(g => g.WardPatientId == request.WardPatientId && g.IsActive, ct);

            if (existingActive is not null)
            {
                return Result<PatientGuardianResponse>.Failure(new Error("PAT_WARD_ALREADY_HAS_GUARDIAN", "Bệnh nhân này đã có người giám hộ hiệu lực. Thu hồi quyền cũ trước khi gán mới."));
            }

            var record = new PatientGuardian
            {
                GuardianPatientId = request.GuardianPatientId,
                WardPatientId = request.WardPatientId,
                Relationship = request.Relationship.Trim(),
                IsActive = true,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = actorUserId,
                Notes = request.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            db.PatientGuardians.Add(record);

            db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                EntityType = "PatientGuardian",
                EntityId = request.WardPatientId,
                Action = AuditActions.PatientGuardianAssigned,
                CreatedAt = DateTime.UtcNow,
                IpAddress = ipAddress,
                Detail = JsonSerializer.Serialize(new
                {
                    guardianPatientId = request.GuardianPatientId,
                    wardPatientId = request.WardPatientId,
                    relationship = request.Relationship
                })
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var actorUser = await db.Users.FirstOrDefaultAsync(u => u.UserId == actorUserId, ct);

            return Result<PatientGuardianResponse>.Success(new PatientGuardianResponse(
                record.PatientGuardianId,
                guardian.PatientId,
                guardian.PatientCode,
                guardian.FullName,
                ward.PatientId,
                ward.PatientCode,
                ward.FullName,
                record.Relationship,
                record.IsActive,
                record.AssignedAt,
                record.AssignedByUserId,
                actorUser?.FullName ?? "Chưa ghi nhận",
                record.RevokedAt,
                record.RevokedByUserId,
                null,
                record.Notes
            ));
        }
        catch (Exception e) when (IsConflict(e))
        {
            return Result<PatientGuardianResponse>.Failure(new Error("PAT_GUARDIAN_CONFLICT", "Thao tác gán giám hộ xung đột đồng thời. Vui lòng thử lại."));
        }
    }

    public async Task<bool> HasActiveGuardianshipAsync(int guardianUserId, int wardPatientId, CancellationToken ct = default)
    {
        return await db.PatientGuardians
            .AnyAsync(g => g.WardPatientId == wardPatientId &&
                           g.IsActive &&
                           g.GuardianPatient!.UserId == guardianUserId, ct);
    }

    public async Task<Result<PatientGuardianResponse>> RevokeGuardianAsync(
        int wardPatientId, int actorUserId, RevokePatientGuardianRequest request, string? ipAddress, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var active = await db.PatientGuardians
                .Include(g => g.GuardianPatient)
                .Include(g => g.WardPatient)
                .Include(g => g.AssignedByUser)
                .FirstOrDefaultAsync(g => g.WardPatientId == wardPatientId && g.IsActive, ct);

            if (active is null || active.PatientGuardianId != request.PatientGuardianId || active.AssignedAt != request.ExpectedAssignedAt.ToUniversalTime())
                return Result<PatientGuardianResponse>.Failure(new Error("PAT_GUARDIAN_CONFLICT", "Quan hệ giám hộ đã thay đổi. Vui lòng tải lại trước khi thu hồi."));

            active.IsActive = false;
            active.RevokedAt = DateTime.UtcNow;
            active.RevokedByUserId = actorUserId;
            active.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                active.Notes = string.IsNullOrWhiteSpace(active.Notes)
                    ? $"[Thu hồi] {request.Notes.Trim()}"
                    : $"{active.Notes} | [Thu hồi] {request.Notes.Trim()}";
            }

            db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                EntityType = "PatientGuardian",
                EntityId = wardPatientId,
                Action = AuditActions.PatientGuardianRevoked,
                CreatedAt = DateTime.UtcNow,
                IpAddress = ipAddress,
                Detail = JsonSerializer.Serialize(new
                {
                    guardianPatientId = active.GuardianPatientId,
                    wardPatientId = active.WardPatientId,
                    notes = request.Notes
                })
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var revoker = await db.Users.FirstOrDefaultAsync(u => u.UserId == actorUserId, ct);

            return Result<PatientGuardianResponse>.Success(new PatientGuardianResponse(
                active.PatientGuardianId,
                active.GuardianPatientId,
                active.GuardianPatient?.PatientCode ?? "",
                active.GuardianPatient?.FullName ?? "",
                active.WardPatientId,
                active.WardPatient?.PatientCode ?? "",
                active.WardPatient?.FullName ?? "",
                active.Relationship,
                active.IsActive,
                active.AssignedAt,
                active.AssignedByUserId,
                active.AssignedByUser?.FullName ?? "",
                active.RevokedAt,
                active.RevokedByUserId,
                revoker?.FullName ?? "Chưa ghi nhận",
                active.Notes
            ));
        }
        catch (Exception e) when (IsConflict(e))
        {
            return Result<PatientGuardianResponse>.Failure(new Error("PAT_GUARDIAN_CONFLICT", "Thao tác thu hồi giám hộ xung đột đồng thời. Vui lòng thử lại."));
        }
    }

    public async Task<Result<PatientResponse>> PromoteWardToIndependentPatientAsync(
        int wardPatientId, PromoteWardRequest request, int actorUserId, string? ipAddress, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var ward = await db.Patients.FirstOrDefaultAsync(p => p.PatientId == wardPatientId, ct);
            if (ward is null || !ward.IsActive)
                return Result<PatientResponse>.Failure(new Error("PAT_WARD_NOT_FOUND", "Hồ sơ bệnh nhân không tồn tại hoặc đã ngừng hoạt động."));

            if (ward.UserId.HasValue)
                return Result<PatientResponse>.Failure(new Error("PAT_WARD_ALREADY_INDEPENDENT", "Bệnh nhân này đã có tài khoản người dùng độc lập."));

            var citizenIdHash = identityHasher.Hash(request.RawCitizenId);
            var legacyHash = CitizenIdIdentity.Hash(request.RawCitizenId);
            if (await db.Patients.AnyAsync(p => p.PatientId != wardPatientId &&
                (p.CitizenIdHash == citizenIdHash || p.CitizenIdHash == legacyHash), ct))
            {
                return Result<PatientResponse>.Failure(new Error("PAT_CITIZEN_ID_DUPLICATE", "CCCD này đã được liên kết với một hồ sơ bệnh nhân khác."));
            }

            var phoneToUse = !string.IsNullOrWhiteSpace(request.Phone) ? request.Phone.Trim() : ward.Phone;

            var patientRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleCode == RoleCodes.Patient, ct);
            if (patientRole is null)
                return Result<PatientResponse>.Failure(Error.ServerError);

            var initialPassword = phoneToUse;
            var passwordHash = passwordHasher.Hash(initialPassword);

            var newUser = new User
            {
                RoleId = patientRole.RoleId,
                Phone = phoneToUse,
                FullName = ward.FullName,
                DateOfBirth = ward.DateOfBirth,
                Gender = ward.Gender,
                PasswordHash = passwordHash,
                IsActive = true,
                LoginByCitizenId = true,
                RequiresPhoneActivation = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(newUser);
            await db.SaveChangesAsync(ct);

            ward.UserId = newUser.UserId;
            ward.Phone = phoneToUse;
            ward.CitizenIdHash = citizenIdHash;
            ward.CitizenIdMasked = identityHasher.Mask(request.RawCitizenId);
            ward.DocumentType = IdentityDocumentType.CitizenId;
            ward.IssuingCountryCode = null;
            ward.HashScheme = IdentityHashScheme.HmacCanonical;
            ward.IdentityKeyVersion = IdentityDocumentConstants.CurrentHmacKeyVersion;
            ward.CitizenIdVersion++;
            ward.UpdatedAt = DateTime.UtcNow;

            var activeGuardians = await db.PatientGuardians
                .Where(g => g.WardPatientId == wardPatientId && g.IsActive)
                .ToListAsync(ct);

            foreach (var g in activeGuardians)
            {
                g.IsActive = false;
                g.RevokedAt = DateTime.UtcNow;
                g.RevokedByUserId = actorUserId;
                g.UpdatedAt = DateTime.UtcNow;
                g.Notes = string.IsNullOrWhiteSpace(g.Notes)
                    ? "[Chấm dứt] Chuyển đổi thành tài khoản độc lập"
                    : $"{g.Notes} | [Chấm dứt] Chuyển đổi thành tài khoản độc lập";
            }

            db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                EntityType = "Patient",
                EntityId = wardPatientId,
                Action = AuditActions.PatientPromotedToIndependent,
                CreatedAt = DateTime.UtcNow,
                IpAddress = ipAddress,
                Detail = JsonSerializer.Serialize(new
                {
                    wardPatientId = wardPatientId,
                    userId = newUser.UserId,
                    revokedGuardiansCount = activeGuardians.Count
                })
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Result<PatientResponse>.Success(new PatientResponse(
                ward.PatientId,
                ward.PatientCode,
                ward.FullName,
                ward.DateOfBirth,
                ward.Gender,
                ward.Phone,
                ward.Email,
                ward.Address,
                ward.IsActive
            ));
        }
        catch (Exception e) when (IsConflict(e))
        {
            return Result<PatientResponse>.Failure(new Error("PAT_PROMOTE_CONFLICT", "Thao tác chuyển đổi tài khoản độc lập xung đột đồng thời. Vui lòng thử lại."));
        }
    }

    private static bool IsConflict(Exception e)
    {
        for (Exception? x = e; x is not null; x = x.InnerException)
        {
            if (x is DbUpdateConcurrencyException || x is PostgresException { SqlState: "40001" or "40P01" or "23505" })
                return true;
        }
        return false;
    }
}

