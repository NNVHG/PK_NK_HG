using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

/// <summary>MOD_MST / F_MST_01: seed danh mục dịch vụ nha khoa và bảng giá ban đầu.</summary>
public static class DentalServicesSeeder
{
    public static async Task SeedAsync(DentalDbContext db, CancellationToken ct = default)
    {
        if (await db.DentalServices.AnyAsync(ct)) return;

        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Role.RoleCode == RoleCodes.Admin, ct);
        var adminId = adminUser?.UserId ?? 1;
        var now = DateTime.UtcNow;

        (string Code, string Name, int Duration, decimal Price, string Desc)[] services =
        [
            ("DV001", "Khám và tư vấn tổng quát", 15, 50000m,
                "Khám răng miệng tổng quát, tư vấn phác đồ điều trị và vệ sinh răng miệng"),
            ("DV002", "Cạo vôi răng và đánh bóng (2 hàm)", 30, 200000m,
                "Lấy sạch cao răng mảng bám trên và dưới nướu bằng máy siêu âm, đánh bóng"),
            ("DV003", "Trám răng thẩm mỹ Composite", 30, 350000m,
                "Phục hồi thân răng sâu, mẻ bằng vật liệu thẩm mỹ Composite quang trùng hợp"),
            ("DV004", "Nhổ răng khôn mọc lệch (tiểu phẫu)", 45, 1200000m,
                "Tiểu phẫu nhổ răng khôn (răng số 8) hàm dưới mọc lệch, ngầm"),
            ("DV005", "Điều trị tủy răng hàm nhiều chân", 60, 1500000m,
                "Lấy tủy, tạo hình và trám bít ống tủy răng cối bằng trâm máy và Gutta-percha"),
            ("DV006", "Tẩy trắng răng tại phòng khám bằng Laser", 45, 2000000m,
                "Tẩy trắng răng công nghệ Laser Whitening kết hợp thuốc tẩy trắng cao cấp"),
            ("DV007", "Chụp phim quanh chóp kỹ thuật số (Periapical)", 10, 80000m,
                "Chụp X-quang răng cận chóp kỹ thuật số khảo sát chân răng và cuống răng"),
            ("DV008", "Bọc răng sứ Cercon HT cao cấp", 60, 4500000m,
                "Phục hình răng sứ toàn phần Cercon HT chính hãng độ chịu lực và thẩm mỹ cao"),
        ];

        db.DentalServices.AddRange(services.Select(s => new DentalService
        {
            Code = s.Code,
            Name = s.Name,
            DurationMinutes = s.Duration,
            Description = s.Desc,
            IsActive = true,
            CreatedAt = now,
            Prices =
            [
                new ServicePrice
                {
                    Amount = s.Price,
                    EffectiveFrom = now.AddDays(-60),
                    CreatedByUserId = adminId,
                    CreatedAt = now,
                }
            ]
        }));

        await db.SaveChangesAsync(ct);
    }
}
