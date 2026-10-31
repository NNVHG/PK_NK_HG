using System.Data;
using System.Text.Json;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data;

/// <summary>
/// Thực thi chuyển tiếp có kiểm soát (D2 — DL-366/DL-367) cho nhóm tài khoản legacy chưa có bằng chứng xác minh SĐT.
/// Hỗ trợ preview/dry-run, kiểm tra an toàn bắt buộc trước khi ghi, thu hồi phiên cũ (TokenVersion++),
/// giữ nguyên PasswordHash/Role/Ownership, ghi audit metadata không chứa PII và chạy lặp an toàn (idempotent).
/// </summary>
public static class LegacyPhoneVerificationTransitionRunner
{
    public const string TransitionAuditAction = "MOD_AUTH_LEGACY_VERIFICATION_REQUIRED";

    public static void AssertNotProductionDatabase(DentalDbContext db)
        => LegacyPhoneVerificationPreflight.AssertNotProductionDatabase(db);

    public sealed record LegacyTransitionResult(
        int TotalInspectedActive,
        int TransitionedCount,
        int TransitionedAdmins,
        int TransitionedStaffNonAdmin,
        int TransitionedPatients,
        int AlreadyPendingLegacyCount,
        int AlreadyVerifiedCount,
        int OtherPendingCount,
        bool IsDryRun);

    public static async Task<LegacyTransitionResult> PreviewAsync(
        DentalDbContext db,
        CancellationToken ct = default)
    {
        AssertNotProductionDatabase(db);
        var inventory = await LegacyPhoneVerificationPreflight.InspectAsync(db, ct);
        EnsureSafeInventory(inventory);

        var activeTotal = inventory.TotalUsers - inventory.InactiveUsers;
        return new LegacyTransitionResult(
            TotalInspectedActive: activeTotal,
            TransitionedCount: inventory.TotalLegacyCandidates,
            TransitionedAdmins: inventory.LegacyUnverifiedAdmins,
            TransitionedStaffNonAdmin: inventory.LegacyUnverifiedStaffNonAdmin,
            TransitionedPatients: inventory.LegacyUnverifiedPatientsEligible,
            AlreadyPendingLegacyCount: inventory.PendingLegacyVerification,
            AlreadyVerifiedCount: inventory.VerifiedActive,
            OtherPendingCount: inventory.PendingInitialActivation + inventory.PendingPhoneChange,
            IsDryRun: true);
    }

    public static async Task<LegacyTransitionResult> TransitionLegacyAccountsAsync(
        DentalDbContext db,
        int actorUserId,
        bool isDryRun = false,
        Func<Task>? beforeCommitHook = null,
        CancellationToken ct = default)
    {
        AssertNotProductionDatabase(db);
        if (actorUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actorUserId), "ActorUserId phải là số nguyên dương.");
        }

        if (isDryRun)
        {
            return await PreviewAsync(db, ct);
        }

        var isRelational = db.Database.IsRelational();
        await using var tx = isRelational
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var committed = false;

        try
        {
            var inventory = await LegacyPhoneVerificationPreflight.InspectAsync(db, ct);
            EnsureSafeInventory(inventory);

            var activeUsers = await db.Users
                .Include(u => u.Role)
                .Where(u => u.IsActive)
                .OrderBy(u => u.UserId)
                .ToListAsync(ct);

            var transitionedAdmins = 0;
            var transitionedStaffNonAdmin = 0;
            var transitionedPatients = 0;
            var alreadyPendingLegacy = 0;
            var alreadyVerified = 0;
            var otherPending = 0;
            var now = DateTime.UtcNow;

            foreach (var user in activeUsers)
            {
                if (user.RequiresPhoneActivation)
                {
                    if (user.PhoneActivationReason == PhoneActivationReason.LegacyVerification)
                    {
                        alreadyPendingLegacy++;
                    }
                    else
                    {
                        otherPending++;
                    }
                    continue;
                }

                if (user.PhoneVerifiedAtUtc.HasValue)
                {
                    alreadyVerified++;
                    continue;
                }

                if (user.TokenVersion == int.MaxValue)
                {
                    throw new InvalidOperationException($"TokenVersion đã đạt giới hạn tối đa cho UserId={user.UserId}.");
                }

                // Chuyển tài khoản legacy sang trạng thái chờ xác minh OTP (giữ nguyên PasswordHash, RoleId, Phone, Ownership)
                user.RequiresPhoneActivation = true;
                user.PhoneActivationReason = PhoneActivationReason.LegacyVerification;
                user.TokenVersion++;
                user.UpdatedAt = now;

                var roleCode = user.Role.RoleCode;
                if (roleCode == RoleCodes.Admin)
                {
                    transitionedAdmins++;
                }
                else if (RoleCodes.StaffRoles.Contains(roleCode))
                {
                    transitionedStaffNonAdmin++;
                }
                else if (roleCode == RoleCodes.Patient)
                {
                    transitionedPatients++;
                }

                db.AuditLogs.Add(new AuditLog
                {
                    UserId = actorUserId,
                    Action = TransitionAuditAction,
                    EntityType = nameof(User),
                    EntityId = user.UserId,
                    Detail = JsonSerializer.Serialize(new
                    {
                        targetUserId = user.UserId,
                        roleCode,
                        activationReason = nameof(PhoneActivationReason.LegacyVerification),
                        previousSessionsRevoked = true,
                        passwordPreserved = true,
                        tokenVersion = user.TokenVersion,
                        decision = "DL-366/DL-367"
                    }),
                    CreatedAt = now
                });
            }

            var totalTransitioned = transitionedAdmins + transitionedStaffNonAdmin + transitionedPatients;
            if (totalTransitioned > 0)
            {
                await db.SaveChangesAsync(ct);
            }

            if (beforeCommitHook is not null)
            {
                await beforeCommitHook();
            }

            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }

            committed = true;

            return new LegacyTransitionResult(
                TotalInspectedActive: activeUsers.Count,
                TransitionedCount: totalTransitioned,
                TransitionedAdmins: transitionedAdmins,
                TransitionedStaffNonAdmin: transitionedStaffNonAdmin,
                TransitionedPatients: transitionedPatients,
                AlreadyPendingLegacyCount: alreadyPendingLegacy,
                AlreadyVerifiedCount: alreadyVerified,
                OtherPendingCount: otherPending,
                IsDryRun: false);
        }
        finally
        {
            if (!committed)
            {
                if (tx is not null)
                {
                    await tx.RollbackAsync(ct);
                }
                db.ChangeTracker.Clear();
            }
        }
    }

    private static void EnsureSafeInventory(LegacyPhoneVerificationInventory inventory)
    {
        if (!inventory.CanTransitionSafely)
        {
            throw new InvalidOperationException(
                $"Chặn chuyển tiếp OTP legacy do chưa đạt điều kiện an toàn: " +
                $"ReachableActiveAdmins={inventory.ReachableActiveAdmins}, " +
                $"InvalidPhoneActiveUsers={inventory.InvalidPhoneActiveUsers}, " +
                $"LegacyUnverifiedPatientsBlocked={inventory.LegacyUnverifiedPatientsBlocked}, " +
                $"InconsistentVerifiedPendingUsers={inventory.InconsistentVerifiedPendingUsers}.");
        }
    }
}
