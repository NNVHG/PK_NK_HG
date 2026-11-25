using System.Security.Cryptography;
using System.Text;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

public static partial class ClinicalDemoDataSeeder
{
    private static async Task SeedWorkspacesAsync(DentalDbContext db, User dentist1, User dentist2, User assistant1, User assistant2, int adminId, DateTime now, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.WorkspaceVersion > 0 || u.WorkspaceRoomId != null || u.WorkspaceDentistId != null, ct)) return;
        var rooms = await db.ClinicRooms.OrderBy(r => r.Id).ToListAsync(ct);
        if (rooms.Count < 2) return;

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, "SE Asia Standard Time"));
        dentist1.WorkspaceRoomId = rooms[0].Id; dentist1.WorkspaceDate = today; dentist1.WorkspaceUpdatedByUserId = adminId; dentist1.WorkspaceVersion = 1; dentist1.WorkspaceUpdatedAt = now;
        if (dentist2.UserId != dentist1.UserId)
        {
            dentist2.WorkspaceRoomId = rooms[1].Id; dentist2.WorkspaceDate = today; dentist2.WorkspaceUpdatedByUserId = adminId; dentist2.WorkspaceVersion = 1; dentist2.WorkspaceUpdatedAt = now;
        }
        assistant1.WorkspaceDentistId = dentist1.UserId; assistant1.WorkspaceUpdatedByUserId = adminId; assistant1.WorkspaceVersion = 1; assistant1.WorkspaceUpdatedAt = now;
        if (assistant2.UserId != assistant1.UserId)
        {
            assistant2.WorkspaceDentistId = dentist2.UserId; assistant2.WorkspaceUpdatedByUserId = adminId; assistant2.WorkspaceVersion = 1; assistant2.WorkspaceUpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task<(Patient P1, Patient P2, Patient P3, Patient P4, Patient P5)> SeedPatientsAsync(
        DentalDbContext db, User u1, User? u2, User? u3, User receptionist1, DateTime now, CancellationToken ct)
    {
        const string cccd1 = "074088001234";
        const string cccd2 = "074095005678";
        const string cccd4 = "074082009012";
        const string cccd5 = "074091003456";

        var p1 = new Patient
        {
            FullName = "Nguyễn Văn An", DateOfBirth = new DateOnly(1988, 5, 12), Gender = "Nam",
            Phone = "0900000005", Email = "nguyenvanan@demo.vn", Address = "45 Lê Lợi, TP. Thủ Dầu Một, Bình Dương",
            CitizenIdHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cccd1))),
            CitizenIdMasked = cccd1[..3] + "******" + cccd1[^3..],
            DocumentType = IdentityDocumentType.CitizenId,
            HashScheme = IdentityHashScheme.HmacCanonical,
            IdentityKeyVersion = IdentityDocumentConstants.CurrentHmacKeyVersion,
            CitizenIdVersion = 1,
            UserId = u1.UserId, IsActive = true, CreatedAt = now.AddDays(-10),
        };
        var p2 = new Patient
        {
            FullName = "Trần Thị Bích", DateOfBirth = new DateOnly(1995, 10, 24), Gender = "Nữ",
            Phone = "0900000021", Email = "tranthibich@demo.vn", Address = "128 Cách Mạng Tháng Tám, Thuận An, Bình Dương",
            CitizenIdHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cccd2))),
            CitizenIdMasked = cccd2[..3] + "******" + cccd2[^3..],
            DocumentType = IdentityDocumentType.CitizenId,
            HashScheme = IdentityHashScheme.HmacCanonical,
            IdentityKeyVersion = IdentityDocumentConstants.CurrentHmacKeyVersion,
            CitizenIdVersion = 1,
            UserId = u2?.UserId, IsActive = true, CreatedAt = now.AddDays(-5),
        };
        var p3 = new Patient
        {
            FullName = "Nguyễn Hoàng Long", DateOfBirth = new DateOnly(2018, 8, 15), Gender = "Nam",
            Phone = "0900000005", Address = "45 Lê Lợi, TP. Thủ Dầu Một, Bình Dương",
            UserId = null, IsActive = true, CreatedAt = now.AddDays(-2),
        };
        var p4 = new Patient
        {
            FullName = "Phạm Đức Huy", DateOfBirth = new DateOnly(1982, 3, 30), Gender = "Nam",
            Phone = "0900000023", Email = "phamduchuy@demo.vn", Address = "72 Đại Lộ Bình Dương, Thủ Dầu Một",
            CitizenIdHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cccd4))),
            CitizenIdMasked = cccd4[..3] + "******" + cccd4[^3..],
            DocumentType = IdentityDocumentType.CitizenId,
            HashScheme = IdentityHashScheme.HmacCanonical,
            IdentityKeyVersion = IdentityDocumentConstants.CurrentHmacKeyVersion,
            CitizenIdVersion = 1,
            UserId = u3?.UserId, IsActive = true, CreatedAt = now.AddDays(-2),
        };
        var p5 = new Patient
        {
            FullName = "Hoàng Thị Mai", DateOfBirth = new DateOnly(1991, 7, 19), Gender = "Nữ",
            Phone = "0900000025", Address = "18 Yersin, Phú Cường, Thủ Dầu Một, Bình Dương",
            CitizenIdHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cccd5))),
            CitizenIdMasked = cccd5[..3] + "******" + cccd5[^3..],
            DocumentType = IdentityDocumentType.CitizenId,
            HashScheme = IdentityHashScheme.HmacCanonical,
            IdentityKeyVersion = IdentityDocumentConstants.CurrentHmacKeyVersion,
            CitizenIdVersion = 1,
            UserId = null, IsActive = true, CreatedAt = now.AddDays(-1),
        };
        u1.FullName = p1.FullName; u1.DateOfBirth = p1.DateOfBirth; u1.Gender = p1.Gender; u1.Email = p1.Email;
        if (u2 is not null) { u2.FullName = p2.FullName; u2.DateOfBirth = p2.DateOfBirth; u2.Gender = p2.Gender; u2.Email = p2.Email; }
        if (u3 is not null) { u3.FullName = p4.FullName; u3.DateOfBirth = p4.DateOfBirth; u3.Gender = p4.Gender; u3.Email = p4.Email; }
        db.Patients.AddRange(p1, p2, p3, p4, p5);
        await db.SaveChangesAsync(ct);

        db.PatientGuardians.Add(new PatientGuardian
        {
            GuardianPatientId = p1.PatientId,
            WardPatientId = p3.PatientId,
            Relationship = "Cha",
            IsActive = true,
            AssignedAt = now.AddDays(-2),
            AssignedByUserId = receptionist1.UserId,
            Notes = "Đã xác minh giấy khai sinh và CCCD người giám hộ tại quầy (dữ liệu mẫu).",
            CreatedAt = now.AddDays(-2),
        });
        await db.SaveChangesAsync(ct);

        return (p1, p2, p3, p4, p5);
    }
}
