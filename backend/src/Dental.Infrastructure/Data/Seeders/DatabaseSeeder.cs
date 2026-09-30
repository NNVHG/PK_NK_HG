using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Data.Seeders;

/// <summary>
/// Seed dữ liệu khởi tạo: 5 vai trò + 1 tài khoản Admin (từ user-secrets).
/// Development only: thêm 4 tài khoản demo nếu Seed:DemoUsers = true.
/// </summary>
public static class DatabaseSeeder
{
    private static readonly (string Code, string Name, string Desc)[] RoleDefs =
    [
        (RoleCodes.Admin,        "Quản trị viên", "Toàn quyền quản trị hệ thống"),
        (RoleCodes.Receptionist, "Lễ tân/Thu ngân", "Quản lý lịch hẹn và thanh toán"),
        (RoleCodes.Dentist,      "Nha sĩ",        "Khám và điều trị"),
        (RoleCodes.Assistant,    "Phụ tá",         "Hỗ trợ khám, quản lý kho"),
        (RoleCodes.Patient,      "Bệnh nhân",      "Đặt lịch, xem hồ sơ cá nhân"),
    ];

    public static async Task SeedAsync(
        DentalDbContext db,
        IConfiguration config,
        ILogger logger,
        bool isDevelopment)
    {
        // --- Seed roles ---
        foreach (var (code, name, desc) in RoleDefs)
        {
            if (!await db.Roles.AnyAsync(r => r.RoleCode == code))
            {
                db.Roles.Add(new Role { RoleCode = code, RoleName = name, Description = desc });
                logger.LogInformation("Seed: thêm vai trò {Code}", code);
            }
        }
        await db.SaveChangesAsync();

        // --- Lấy roleId sau khi đã seed ---
        var roleMap = await db.Roles.ToDictionaryAsync(r => r.RoleCode, r => r.RoleId);

        // --- Seed Admin account (bắt buộc) ---
        var adminPhone    = config["Seed:AdminPhone"];
        var adminPassword = config["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminPhone) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("Seed: thiếu Seed:AdminPhone hoặc Seed:AdminPassword — bỏ qua tạo Admin.");
        }
        else if (!await db.Users.AnyAsync(u => u.Phone == adminPhone))
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(adminPassword, 12);
            db.Users.Add(new User
            {
                Phone        = adminPhone,
                PasswordHash = hash,
                FullName     = "Quản trị viên",
                RoleId       = roleMap[RoleCodes.Admin],
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow,
            });
            logger.LogInformation("Seed: tạo tài khoản Admin (SĐT che: {Masked})", MaskPhone(adminPhone));
            await db.SaveChangesAsync();
        }

        // --- Seed demo users (Development only) ---
        if (!isDevelopment) return;

        var demoEnabled  = config.GetValue<bool>("Seed:DemoUsers");
        var demoPassword = config["Seed:DemoPassword"];

        if (!demoEnabled || string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogInformation("Seed demo: tắt hoặc thiếu Seed:DemoPassword — bỏ qua.");
            return;
        }

        var demoHash = BCrypt.Net.BCrypt.HashPassword(demoPassword, 12);

        (string Phone, string Name, string Role)[] demos =
        [
            ("0900000002", "Lễ tân Demo",  RoleCodes.Receptionist),
            ("0900000003", "Nha sĩ Demo",  RoleCodes.Dentist),
            ("0900000004", "Phụ tá Demo",  RoleCodes.Assistant),
            ("0900000005", "Bệnh nhân Demo", RoleCodes.Patient),
        ];

        foreach (var (phone, name, roleCode) in demos)
        {
            if (!await db.Users.AnyAsync(u => u.Phone == phone))
            {
                db.Users.Add(new User
                {
                    Phone        = phone,
                    PasswordHash = demoHash,
                    FullName     = name,
                    RoleId       = roleMap[roleCode],
                    IsActive     = true,
                    CreatedAt    = DateTime.UtcNow,
                });
                logger.LogInformation("Seed demo: tạo {Name} ({RoleCode})", name, roleCode);
            }
        }

        await db.SaveChangesAsync();
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 5) return "***";
        return phone[..2] + new string('*', phone.Length - 5) + phone[^3..];
    }
}
