using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

/// <summary>MOD_BIL / F_BIL_02: seed danh mục mẫu giảm giá do Admin cấu hình (DL-119).</summary>
public static class DiscountPresetsSeeder
{
    public static async Task SeedAsync(DentalDbContext db, CancellationToken ct = default)
    {
        if (await db.DiscountPresets.AnyAsync(ct)) return;

        (string Code, string Name, string Type, decimal Value)[] presets =
        [
            ("KM10",   "Giảm 10% điều trị",                 "Percentage",  10m),
            ("KM05",   "Giảm 5% học sinh sinh viên",        "Percentage",  5m),
            ("KM15",   "Tri ân khách hàng thân thiết 15%",  "Percentage",  15m),
            ("FIX50",  "Ưu đãi 50.000 VNĐ",                 "FixedAmount", 50000m),
            ("FIX100", "Ưu đãi 100.000 VNĐ",                "FixedAmount", 100000m),
        ];

        db.DiscountPresets.AddRange(presets.Select(p => new DiscountPreset
        {
            Code = p.Code,
            Name = p.Name,
            DiscountType = p.Type,
            Value = p.Value,
            IsActive = true,
        }));

        await db.SaveChangesAsync(ct);
    }
}
