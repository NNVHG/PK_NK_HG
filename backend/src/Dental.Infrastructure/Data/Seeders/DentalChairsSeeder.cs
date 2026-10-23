using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

/// <summary>MOD_MST / F_MST_05: seed danh mục phòng và ghế điều trị ban đầu (DL-104).</summary>
public static class DentalChairsSeeder
{
    public static async Task SeedAsync(DentalDbContext db, CancellationToken ct = default)
    {
        if (await db.DentalChairs.AnyAsync(ct)) return;

        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Role.RoleCode == RoleCodes.Admin, ct);
        var adminId = adminUser?.UserId ?? 1;
        var now = DateTime.UtcNow;

        (string ChairNumber, string RoomName, string Description)[] chairs =
        [
            ("G01", "Phòng Khám 1", "Ghế nha khoa tổng quát 1"),
            ("G02", "Phòng Khám 2", "Ghế nha khoa tổng quát 2"),
            ("G03", "Phòng Tiểu Phẫu", "Ghế phẫu thuật và cấy ghép Implant"),
            ("G04", "Phòng Điều Trị 2", "Ghế chỉnh nha và điều trị tủy"),
        ];

        db.DentalChairs.AddRange(chairs.Select(c => new DentalChair
        {
            ChairNumber = c.ChairNumber,
            RoomName = c.RoomName,
            Status = DentalChairStatus.Available,
            Description = c.Description,
            IsActive = true,
            CreatedByUserId = adminId,
            Version = 1,
            CreatedAt = now,
        }));

        await db.SaveChangesAsync(ct);
    }
}
