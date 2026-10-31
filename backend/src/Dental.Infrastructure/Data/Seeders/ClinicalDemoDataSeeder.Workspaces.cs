using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

public static partial class ClinicalDemoDataSeeder
{
    private static async Task SeedWorkspacesAsync(DentalDbContext db, User dentist1, User dentist2, User assistant1, User assistant2, int adminId, DateTime now, CancellationToken ct)
    {
        if (await db.StaffWorkspaces.AnyAsync(ct)) return;
        var chairs = await db.DentalChairs.OrderBy(c => c.Id).ToListAsync(ct);
        if (chairs.Count < 2) return;

        db.StaffWorkspaces.Add(new StaffWorkspace { UserId = dentist1.UserId, ChairId = chairs[0].Id, RoomId = chairs[0].RoomId, UpdatedByUserId = adminId, Version = 1, CreatedAt = now });
        if (dentist2.UserId != dentist1.UserId)
            db.StaffWorkspaces.Add(new StaffWorkspace { UserId = dentist2.UserId, ChairId = chairs[1].Id, RoomId = chairs[1].RoomId, UpdatedByUserId = adminId, Version = 1, CreatedAt = now });
        db.StaffWorkspaces.Add(new StaffWorkspace { UserId = assistant1.UserId, DentistId = dentist1.UserId, UpdatedByUserId = adminId, Version = 1, CreatedAt = now });
        if (assistant2.UserId != assistant1.UserId)
            db.StaffWorkspaces.Add(new StaffWorkspace { UserId = assistant2.UserId, DentistId = dentist2.UserId, UpdatedByUserId = adminId, Version = 1, CreatedAt = now });
        await db.SaveChangesAsync(ct);
    }

    private static async Task<(Patient P1, Patient P2, Patient P3, Patient P4)> SeedPatientsAsync(DentalDbContext db, User u1, User? u2, User? u3, DateTime now, CancellationToken ct)
    {
        var p1 = new Patient
        {
            FullName = "Nguyễn Văn An", DateOfBirth = new DateOnly(1988, 5, 12), Gender = "Nam",
            Phone = "0900000005", Email = "nguyenvanan@demo.vn", Address = "45 Lê Lợi, TP. Thủ Dầu Một, Bình Dương",
            UserId = u1.UserId, IsActive = true, CreatedAt = now.AddDays(-10),
        };
        var p2 = new Patient
        {
            FullName = "Trần Thị Bích", DateOfBirth = new DateOnly(1995, 10, 24), Gender = "Nữ",
            Phone = "0900000021", Email = "tranthibich@demo.vn", Address = "128 Cách Mạng Tháng Tám, Thuận An, Bình Dương",
            UserId = u2?.UserId, IsActive = true, CreatedAt = now.AddDays(-5),
        };
        var p3 = new Patient
        {
            FullName = "Lê Hoàng Long", DateOfBirth = new DateOnly(2018, 8, 15), Gender = "Nam",
            Phone = "0900000024", Address = "72 Đại Lộ Bình Dương, Thủ Dầu Một",
            UserId = null, IsActive = true, CreatedAt = now.AddDays(-2),
        };
        var p4 = new Patient
        {
            FullName = "Phạm Đức Huy", DateOfBirth = new DateOnly(1982, 3, 30), Gender = "Nam",
            Phone = "0900000023", Email = "phamduchuy@demo.vn", Address = "72 Đại Lộ Bình Dương, Thủ Dầu Một",
            UserId = u3?.UserId, IsActive = true, CreatedAt = now.AddDays(-2),
        };
        db.Patients.AddRange(p1, p2, p3, p4);
        await db.SaveChangesAsync(ct);
        return (p1, p2, p3, p4);
    }
}
