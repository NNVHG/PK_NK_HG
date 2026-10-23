using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

/// <summary>MOD_MST / F_MST_04: seed danh mục vật tư y tế và ngưỡng tồn tối thiểu ban đầu.</summary>
public static class MedicalSuppliesSeeder
{
    public static async Task SeedAsync(DentalDbContext db, CancellationToken ct = default)
    {
        if (await db.MedicalSupplies.AnyAsync(ct)) return;

        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Role.RoleCode == RoleCodes.Admin, ct);
        var adminId = adminUser?.UserId ?? 1;
        var now = DateTime.UtcNow;

        (string Code, string Name, string Unit, int MinStock, string Desc)[] supplies =
        [
            ("VT001", "Găng tay y tế cao su có bột (hộp 100 cái)", "Hộp", 10,
                "Găng tay cao su y tế tiệt trùng size S/M"),
            ("VT002", "Khẩu trang y tế 4 lớp kháng khuẩn (hộp 50 cái)", "Hộp", 15,
                "Khẩu trang y tế 4 lớp lọc khuẩn chuẩn y khoa"),
            ("VT003", "Kim tiêm tê nha khoa ngắn 27G (hộp 100 cây)", "Hộp", 5,
                "Kim tiêm gây tê cục bộ dùng cho ống tiêm nha khoa"),
            ("VT004", "Ống hút nước bọt dùng một lần (bịch 100 ống)", "Bịch", 10,
                "Ống hút nước bọt uốn dẻo nha khoa"),
            ("VT005", "Composite đặc trám răng Filtek Z250 (tuýp 4g)", "Tuýp", 8,
                "Vật liệu trám răng thẩm mỹ quang trùng hợp màu A2/A3"),
            ("VT006", "Gòn cuộn nha khoa vô trùng (bịch 500 viên)", "Bịch", 10,
                "Gòn cuộn cách ly nước bọt nha khoa"),
            ("VT007", "Chỉ tự tiêu phẫu thuật Vicryl 4/0 (tép)", "Tép", 20,
                "Chỉ khâu phẫu thuật tiêu chậm có kim cắt tròn"),
            ("VT008", "Mũi khoan kim cương mở tủy (bộ 5 mũi)", "Bộ", 5,
                "Mũi khoan Mani mở tủy và sửa soạn xoang"),
        ];

        db.MedicalSupplies.AddRange(supplies.Select(s => new MedicalSupply
        {
            Code = s.Code,
            Name = s.Name,
            Unit = s.Unit,
            MinimumStockThreshold = s.MinStock,
            Description = s.Desc,
            SearchText = DiagnosisSearch.Normalize($"{s.Code} {s.Name}"),
            IsActive = true,
            CreatedByUserId = adminId,
            Version = 1,
            CreatedAt = now,
        }));

        await db.SaveChangesAsync(ct);
    }
}
