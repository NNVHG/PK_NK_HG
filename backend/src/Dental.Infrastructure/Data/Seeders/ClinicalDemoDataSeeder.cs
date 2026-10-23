using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Data.Seeders;

/// <summary>
/// Seed kịch bản lâm sàng mẫu (Development only).
/// </summary>
public static partial class ClinicalDemoDataSeeder
{
    public static async Task SeedAsync(DentalDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (await db.Patients.AnyAsync(ct)) return;

        var users = await db.Users.ToDictionaryAsync(u => u.Phone, u => u, ct);
        if (!users.TryGetValue("0900000002", out var receptionist1) ||
            !users.TryGetValue("0900000003", out var dentist1) ||
            !users.TryGetValue("0900000004", out var assistant1) ||
            !users.TryGetValue("0900000005", out var patientUser1))
        {
            logger.LogWarning("ClinicalDemoDataSeeder: Thiếu các tài khoản demo cơ bản, bỏ qua seed lâm sàng.");
            return;
        }

        var dentist2 = users.GetValueOrDefault("0900000013") ?? dentist1;
        var assistant2 = users.GetValueOrDefault("0900000014") ?? assistant1;
        var patientUser2 = users.GetValueOrDefault("0900000021");
        var patientUser3 = users.GetValueOrDefault("0900000023");
        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Role.RoleCode == RoleCodes.Admin, ct);
        var adminId = adminUser?.UserId ?? dentist1.UserId;

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        // 1. Phân công ghế và phòng khám (MOD_MST / MOD_ASS)
        await SeedWorkspacesAsync(db, dentist1, dentist2, assistant1, assistant2, adminId, now, ct);

        // 2. Hồ sơ bệnh nhân (MOD_PAT)
        var (p1, p2, p3, p4) = await SeedPatientsAsync(db, patientUser1, patientUser2, patientUser3, now, ct);

        // 3. Lịch hẹn, Hàng đợi tiếp đón, Ca khám (MOD_APP / MOD_CHK)
        var (appt1, appt2, v1, v2) = await SeedVisitsAndQueueAsync(db, p1, p2, p3, p4, dentist1, dentist2, receptionist1, today, now, ct);

        // 4. Nghiệp vụ chuyên môn, Dược, Tài chính & Kho (MOD_FDI / MOD_RX / MOD_BIL / MOD_INV)
        await SeedClinicalAndInventoryAsync(db, p1, p2, v1, v2, dentist1, receptionist1, assistant1, adminId, today, now, ct);

        logger.LogInformation("ClinicalDemoDataSeeder: Hoàn tất seed dữ liệu lâm sàng và vận hành demo.");
    }
}
