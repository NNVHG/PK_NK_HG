using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

/// <summary>MOD_MST / F_MST_05: seed danh mục phòng điều trị ban đầu (DL-104, DL-361, DL-367).</summary>
public static class DentalChairsSeeder
{
    public static async Task SeedAsync(DentalDbContext db, CancellationToken ct = default)
    {
        if (await db.ClinicRooms.AnyAsync(ct)) return;

        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Role.RoleCode == RoleCodes.Admin, ct);
        var adminId = adminUser?.UserId ?? 1;
        var now = DateTime.UtcNow;

        (string RoomCode, string RoomName, string Description)[] rooms =
        [
            ("G01", "Phòng Khám 1", "Phòng nha khoa tổng quát 1"),
            ("G02", "Phòng Khám 2", "Phòng nha khoa tổng quát 2"),
            ("G03", "Phòng Tiểu Phẫu", "Phòng phẫu thuật và cấy ghép Implant"),
            ("G04", "Phòng Điều Trị 2", "Phòng chỉnh nha và điều trị tủy"),
        ];

        var entities = rooms.Select(r => new ClinicRoom
        {
            RoomCode = r.RoomCode,
            RoomName = ClinicRoom.CleanDisplayRoomName(r.RoomName),
            NormalizedRoomName = ClinicRoom.NormalizeRoomName(r.RoomName),
            Status = DentalChairStatus.Available,
            Description = r.Description,
            IsActive = true,
            CreatedByUserId = adminId,
            Version = 1,
            CreatedAt = now,
        }).ToList();

        db.ClinicRooms.AddRange(entities);
        await db.SaveChangesAsync(ct);
    }
}
