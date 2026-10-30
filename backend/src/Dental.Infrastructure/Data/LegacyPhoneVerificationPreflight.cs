using System.Text.RegularExpressions;
using Dental.Domain.Constants;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data;

/// <summary>
/// Kiểm kê chỉ đọc các tài khoản thiếu bằng chứng xác minh số điện thoại (DL-366/DL-367).
/// Phân tách rõ: tài khoản đã xác minh, chờ kích hoạt mới (InitialActivation), chờ xác minh đổi số (PhoneChange),
/// đã đánh dấu LegacyVerification, và nhóm legacy chưa xác minh (Admin, Nhân viên khác, Bệnh nhân hợp lệ / bị chặn).
/// Tuyệt đối không ghi giả PhoneVerifiedAtUtc, không thay đổi trạng thái hay trả về dữ liệu nhạy cảm (SĐT/CCCD/Hash).
/// </summary>
public static class LegacyPhoneVerificationPreflight
{
    private static readonly Regex PhonePattern = new("^0[0-9]{9}$", RegexOptions.Compiled);
    private static readonly Regex HashPattern = new("^[0-9A-Fa-f]{64}$", RegexOptions.Compiled);
    private static readonly Regex CountryPattern = new(IdentityDocumentConstants.NationalityCodeRegex, RegexOptions.Compiled);

    public static void AssertNotProductionDatabase(DentalDbContext db)
    {
        var dbName = db.Database.GetDbConnection().Database;
        if (string.Equals(dbName, "dental_db", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Guard: Thao tác kiểm kê/chuẩn bị chuyển tiếp OTP trực tiếp trên database production 'dental_db' bị từ chối.");
        }
    }

    public static async Task<LegacyPhoneVerificationInventory> InspectSafeAsync(
        DentalDbContext db,
        CancellationToken ct = default)
    {
        AssertNotProductionDatabase(db);
        return await InspectAsync(db, ct);
    }

    public static async Task<LegacyPhoneVerificationInventory> InspectAsync(
        DentalDbContext db,
        CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking()
            .Select(u => new
            {
                u.UserId,
                u.Phone,
                u.IsActive,
                u.RequiresPhoneActivation,
                u.PhoneActivationReason,
                u.PhoneVerifiedAtUtc,
                u.LoginByCitizenId,
                RoleCode = u.Role.RoleCode
            })
            .ToListAsync(ct);

        var patientLinks = await db.Patients.AsNoTracking()
            .Where(p => p.UserId != null)
            .Select(p => new
            {
                UserId = p.UserId!.Value,
                p.IsActive,
                p.CitizenIdHash,
                p.DocumentType,
                p.IssuingCountryCode
            })
            .ToListAsync(ct);

        var patientsByUser = patientLinks
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var inactiveUsers = 0;
        var verifiedActive = 0;
        var pendingInitialActivation = 0;
        var pendingPhoneChange = 0;
        var pendingLegacyVerification = 0;
        var legacyUnverifiedAdmins = 0;
        var legacyUnverifiedStaffNonAdmin = 0;
        var legacyUnverifiedPatientsEligible = 0;
        var legacyUnverifiedPatientsBlocked = 0;
        var invalidPhoneActiveUsers = 0;
        var inconsistentVerifiedPendingUsers = 0;
        var reachableActiveAdmins = 0;

        foreach (var user in users)
        {
            if (!user.IsActive)
            {
                inactiveUsers++;
                continue;
            }

            var phone = (user.Phone ?? string.Empty).Trim();
            var hasValidPhone = PhonePattern.IsMatch(phone);
            if (!hasValidPhone)
            {
                invalidPhoneActiveUsers++;
            }

            if (user.RoleCode == RoleCodes.Admin && hasValidPhone)
            {
                reachableActiveAdmins++;
            }

            if (user.RequiresPhoneActivation)
            {
                if (user.PhoneVerifiedAtUtc.HasValue)
                {
                    inconsistentVerifiedPendingUsers++;
                }

                switch (user.PhoneActivationReason)
                {
                    case PhoneActivationReason.PhoneChange:
                        pendingPhoneChange++;
                        break;
                    case PhoneActivationReason.LegacyVerification:
                        pendingLegacyVerification++;
                        break;
                    case null:
                    case PhoneActivationReason.InitialActivation:
                    default:
                        pendingInitialActivation++;
                        break;
                }
                continue;
            }

            if (user.PhoneVerifiedAtUtc.HasValue)
            {
                verifiedActive++;
                continue;
            }

            // Nhóm tài khoản cũ (legacy): IsActive = true, RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null
            if (user.RoleCode == RoleCodes.Admin)
            {
                if (hasValidPhone)
                {
                    legacyUnverifiedAdmins++;
                }
            }
            else if (RoleCodes.StaffRoles.Contains(user.RoleCode))
            {
                if (hasValidPhone)
                {
                    legacyUnverifiedStaffNonAdmin++;
                }
            }
            else if (user.RoleCode == RoleCodes.Patient)
            {
                var hasEligiblePatientIdentity = false;
                if (hasValidPhone && user.LoginByCitizenId &&
                    patientsByUser.TryGetValue(user.UserId, out var linkedPatients))
                {
                    var activeProfiles = linkedPatients.Where(p => p.IsActive).ToList();
                    if (activeProfiles.Count == 1)
                    {
                        var profile = activeProfiles[0];
                        var validHash = !string.IsNullOrWhiteSpace(profile.CitizenIdHash) &&
                                        HashPattern.IsMatch(profile.CitizenIdHash);
                        var validCountry = profile.DocumentType != IdentityDocumentType.Passport ||
                                           (!string.IsNullOrWhiteSpace(profile.IssuingCountryCode) &&
                                            CountryPattern.IsMatch(profile.IssuingCountryCode));
                        var validCitizenCountry = profile.DocumentType != IdentityDocumentType.CitizenId ||
                                                  profile.IssuingCountryCode is null;
                        hasEligiblePatientIdentity = validHash && validCountry && validCitizenCountry;
                    }
                }

                if (hasEligiblePatientIdentity)
                {
                    legacyUnverifiedPatientsEligible++;
                }
                else
                {
                    legacyUnverifiedPatientsBlocked++;
                }
            }
        }

        return new LegacyPhoneVerificationInventory(
            TotalUsers: users.Count,
            InactiveUsers: inactiveUsers,
            VerifiedActive: verifiedActive,
            PendingInitialActivation: pendingInitialActivation,
            PendingPhoneChange: pendingPhoneChange,
            PendingLegacyVerification: pendingLegacyVerification,
            LegacyUnverifiedAdmins: legacyUnverifiedAdmins,
            LegacyUnverifiedStaffNonAdmin: legacyUnverifiedStaffNonAdmin,
            LegacyUnverifiedPatientsEligible: legacyUnverifiedPatientsEligible,
            LegacyUnverifiedPatientsBlocked: legacyUnverifiedPatientsBlocked,
            InvalidPhoneActiveUsers: invalidPhoneActiveUsers,
            InconsistentVerifiedPendingUsers: inconsistentVerifiedPendingUsers,
            ReachableActiveAdmins: reachableActiveAdmins);
    }
}

/// <summary>
/// Kết quả kiểm kê tổng hợp trạng thái xác minh số điện thoại tài khoản; không chứa PII hay định danh.
/// </summary>
public sealed record LegacyPhoneVerificationInventory(
    int TotalUsers,
    int InactiveUsers,
    int VerifiedActive,
    int PendingInitialActivation,
    int PendingPhoneChange,
    int PendingLegacyVerification,
    int LegacyUnverifiedAdmins,
    int LegacyUnverifiedStaffNonAdmin,
    int LegacyUnverifiedPatientsEligible,
    int LegacyUnverifiedPatientsBlocked,
    int InvalidPhoneActiveUsers,
    int InconsistentVerifiedPendingUsers,
    int ReachableActiveAdmins)
{
    public int TotalLegacyCandidates =>
        LegacyUnverifiedAdmins + LegacyUnverifiedStaffNonAdmin + LegacyUnverifiedPatientsEligible;

    public bool CanTransitionSafely =>
        ReachableActiveAdmins >= 1 &&
        InvalidPhoneActiveUsers == 0 &&
        LegacyUnverifiedPatientsBlocked == 0 &&
        InconsistentVerifiedPendingUsers == 0;
}
