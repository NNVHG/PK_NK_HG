using System.Text.Json;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

public static partial class ClinicalDemoDataSeeder
{
    private static async Task SeedClinicalAndInventoryAsync(
        DentalDbContext db, Patient p1, Patient p2, Patient p4, Visit v1, Visit v2, Visit v3, Visit v4,
        User dentist1, User dentist2, User receptionist1, User assistant1, int adminId,
        DateOnly today, DateTime now, CancellationToken ct)
    {
        // 1. Tiền sử dị ứng & Sinh hiệu (MOD_PAT)
        var historyRecords = new MedicalHistoryRecord[]
        {
            new()
            {
                PatientId = p1.PatientId, VisitId = v1.VisitId, RecordedByUserId = assistant1.UserId, CreatedAt = now.Date.AddHours(8).AddMinutes(30),
                Items =
                [
                    new MedicalHistoryItem { Type = MedicalHistoryTypes.Allergy, Name = "Penicillin", IsCritical = true, Detail = "Nổi mề đay cấp tính, phù Quincke khi tiêm kháng sinh nhóm beta-lactam" },
                    new MedicalHistoryItem { Type = MedicalHistoryTypes.Condition, Name = "Tăng huyết áp", IsCritical = false, Detail = "Huyết áp vô căn độ 1, đang uống thuốc duy trì Amlodipine" }
                ]
            },
            new()
            {
                PatientId = p2.PatientId, VisitId = v2.VisitId, RecordedByUserId = assistant1.UserId, CreatedAt = now.Date.AddHours(9).AddMinutes(22),
                Items =
                [
                    new MedicalHistoryItem { Type = MedicalHistoryTypes.Allergy, Name = "Aspirin", IsCritical = true, Detail = "Co thắt phế quản, khó thở" }
                ]
            },
            new()
            {
                PatientId = p4.PatientId, VisitId = v3.VisitId, RecordedByUserId = assistant1.UserId, CreatedAt = now.Date.AddDays(-2).AddHours(10),
                Items =
                [
                    new MedicalHistoryItem { Type = MedicalHistoryTypes.Condition, Name = "Tiểu đường type 2", IsCritical = true, Detail = "Đang dùng Metformin, cần theo dõi đường huyết trước tiểu phẫu" }
                ]
            }
        };
        foreach (var h in historyRecords) h.SyncItemsJson();
        db.MedicalHistoryRecords.AddRange(historyRecords);

        db.VitalSignRecords.AddRange(
            new VitalSignRecord
            {
                PatientId = p1.PatientId, VisitId = v1.VisitId, SystolicBp = 145, DiastolicBp = 92, PulseBpm = 84, TemperatureC = 36.8m,
                Note = "Huyết áp cao trước thủ thuật (kích hoạt cảnh báo an toàn lâm sàng)", RecordedByUserId = assistant1.UserId, CreatedAt = now.Date.AddHours(8).AddMinutes(30),
            },
            new VitalSignRecord
            {
                PatientId = p2.PatientId, VisitId = v2.VisitId, SystolicBp = 118, DiastolicBp = 78, PulseBpm = 75, TemperatureC = 36.6m,
                Note = "Sinh hiệu ổn định", RecordedByUserId = assistant1.UserId, CreatedAt = now.Date.AddHours(9).AddMinutes(22),
            },
            new VitalSignRecord
            {
                PatientId = p4.PatientId, VisitId = v3.VisitId, SystolicBp = 126, DiastolicBp = 80, PulseBpm = 76, TemperatureC = 36.7m,
                Note = "Đo trước điều trị tủy", RecordedByUserId = assistant1.UserId, CreatedAt = now.Date.AddDays(-2).AddHours(10),
            }
        );
        await db.SaveChangesAsync(ct);

        // 2. Sơ đồ răng FDI (MOD_FDI)
        db.ToothConditions.AddRange(
            new ToothCondition { VisitId = v1.VisitId, ToothNumber = 36, Surface = "O", ConditionCode = "CARIES", Note = "Sâu men ngà nông xoang I mặt nhai", CreatedAt = now.Date.AddHours(8).AddMinutes(40) },
            new ToothCondition { VisitId = v1.VisitId, ToothNumber = 36, Surface = "O", ConditionCode = "RESTORED", Note = "Trám Composite quang trùng hợp hoàn thiện", CreatedAt = now.Date.AddHours(9).AddMinutes(0) },
            new ToothCondition { VisitId = v1.VisitId, ToothNumber = 48, Surface = null, ConditionCode = "IMPACTED", Note = "Răng khôn mọc lệch nghiêng gần 45 độ, đau khi giắt thức ăn", CreatedAt = now.Date.AddHours(8).AddMinutes(42) },
            new ToothCondition { VisitId = v2.VisitId, ToothNumber = 21, Surface = "M", ConditionCode = "RESTORED", Note = "Đang tạo hình xoang trám phục hồi góc cắn", CreatedAt = now.Date.AddHours(9).AddMinutes(30) },
            new ToothCondition { VisitId = v3.VisitId, ToothNumber = 46, Surface = "O", ConditionCode = "ROOT_CANAL", Note = "Viêm tủy không hồi phục, đã mở tủy và băng thuốc", CreatedAt = now.Date.AddDays(-2).AddHours(10).AddMinutes(20) },
            new ToothCondition { VisitId = v4.VisitId, ToothNumber = 16, Surface = "B", ConditionCode = "CARIES", Note = "Sâu cổ răng mặt ngoài, chờ hoàn tất khám ca chiều", CreatedAt = now.Date.AddHours(9).AddMinutes(55) }
        );
        await db.SaveChangesAsync(ct);

        // Ghi chú hậu điều trị (MOD_PAT / F_PAT_08)
        v1.PostTreatmentNotesJson = JsonSerializer.Serialize(new[]
        {
            new PostTreatmentNote
            {
                Id = 1,
                VisitId = v1.VisitId,
                CreatedByUserId = dentist1.UserId,
                Note = "Bệnh nhân dung nạp thủ thuật tốt, đã hướng dẫn vệ sinh kẽ răng và hẹn tái kiểm tra sau 6 tháng.",
                CreatedAt = now.Date.AddHours(9).AddMinutes(25),
                RequestId = Guid.NewGuid(),
            }
        });
        await db.SaveChangesAsync(ct);

        // 3. Chỉ định dịch vụ theo răng (MOD_FDI)
        var services = await db.DentalServices.ToDictionaryAsync(s => s.Code, s => s, ct);
        var dv003 = services["DV003"];
        var dv002 = services["DV002"];
        var dv005 = services.GetValueOrDefault("DV005") ?? dv003;
        var p003 = dv003.GetPriceHistory().OrderByDescending(p => p.EffectiveFrom).First();
        var p002 = dv002.GetPriceHistory().OrderByDescending(p => p.EffectiveFrom).First();
        var p005 = dv005.GetPriceHistory().OrderByDescending(p => p.EffectiveFrom).First();

        var vs1 = new VisitService { VisitId = v1.VisitId, ServiceId = dv003.DentalServiceId, ServicePriceId = p003.ServicePriceId, ServiceCode = dv003.Code, ServiceName = dv003.Name, ToothNumber = 36, Surface = "O", Quantity = 1, UnitPrice = p003.Amount, CreatedByUserId = dentist1.UserId, CreatedAt = now.Date.AddHours(8).AddMinutes(45) };
        var vs2 = new VisitService { VisitId = v1.VisitId, ServiceId = dv002.DentalServiceId, ServicePriceId = p002.ServicePriceId, ServiceCode = dv002.Code, ServiceName = dv002.Name, Quantity = 1, UnitPrice = p002.Amount, CreatedByUserId = dentist1.UserId, CreatedAt = now.Date.AddHours(8).AddMinutes(50) };
        var vs3 = new VisitService { VisitId = v2.VisitId, ServiceId = dv003.DentalServiceId, ServicePriceId = p003.ServicePriceId, ServiceCode = dv003.Code, ServiceName = dv003.Name, ToothNumber = 21, Surface = "M", Quantity = 1, UnitPrice = p003.Amount, CreatedByUserId = dentist1.UserId, CreatedAt = now.Date.AddHours(9).AddMinutes(35) };
        var vs4 = new VisitService { VisitId = v3.VisitId, ServiceId = dv005.DentalServiceId, ServicePriceId = p005.ServicePriceId, ServiceCode = dv005.Code, ServiceName = dv005.Name, ToothNumber = 46, Surface = "O", Quantity = 1, UnitPrice = p005.Amount, CreatedByUserId = dentist2.UserId, CreatedAt = now.Date.AddDays(-2).AddHours(10).AddMinutes(25) };
        db.VisitServices.AddRange(vs1, vs2, vs3, vs4);
        await db.SaveChangesAsync(ct);

        // 4. Đơn thuốc, Hóa đơn & Kho (MOD_RX / MOD_BIL / MOD_INV / MOD_AUTH Audit)
        await SeedRxAndInvoiceAsync(db, v1, v3, vs1, vs2, vs4, dentist1, dentist2, receptionist1, assistant1, adminId, today, now, ct);
    }
}
