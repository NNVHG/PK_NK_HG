using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

/// <summary>MOD_MST / F_MST_06: seed cấu hình hệ thống ban đầu (DL-105, DL-174, DL-379).</summary>
public static class SystemConfigsSeeder
{
    public static async Task SeedAsync(DentalDbContext db, CancellationToken ct = default)
    {
        (string Key, string Value, string Desc)[] defaults =
        [
            ("Clinic:Address", "123 Đường 30/4, Phường Phú Hòa, TP. Thủ Dầu Một, Bình Dương",
                "Địa chỉ phòng khám hiển thị trên biên lai và phiếu in"),
            ("Clinic:Phone", "0274 3822 567",
                "Số điện thoại hotline phòng khám"),
            ("Clinic:WorkStartTime", "08:00",
                "Giờ mở cửa tiếp nhận khám"),
            ("Clinic:LunchStartTime", "12:00",
                "Giờ bắt đầu nghỉ trưa"),
            ("Clinic:LunchEndTime", "13:30",
                "Giờ kết thúc nghỉ trưa"),
            ("Clinic:WorkEndTime", "17:30",
                "Giờ đóng cửa phòng khám"),
        ];

        foreach (var (key, value, desc) in defaults)
        {
            if (!await db.SystemConfigs.AnyAsync(s => s.Key == key, ct))
            {
                db.SystemConfigs.Add(new SystemConfig
                {
                    Key = key,
                    Value = value,
                    Description = desc,
                    DataType = "String",
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
