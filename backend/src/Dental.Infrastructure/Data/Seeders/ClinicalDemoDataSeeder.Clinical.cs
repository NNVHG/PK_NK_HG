using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

public static partial class ClinicalDemoDataSeeder
{
    private static async Task SeedClinicalAndInventoryAsync(
        DentalDbContext db, Patient p1, Patient p2, Visit v1, Visit v2,
        User dentist1, User receptionist1, User assistant1, int adminId,
        DateOnly today, DateTime now, CancellationToken ct)
    {
        // 1. Tiền sử dị ứng & Sinh hiệu (MOD_PAT)
        db.MedicalHistoryRecords.AddRange(
            new MedicalHistoryRecord
            {
                PatientId = p1.PatientId, VisitId = v1.VisitId, RecordedByUserId = assistant1.UserId, CreatedAt = now.Date.AddHours(8).AddMinutes(30),
                Items =
                [
                    new MedicalHistoryItem { Type = MedicalHistoryTypes.Allergy, Name = "Penicillin", IsCritical = true, Detail = "Nổi mề đay cấp tính, phù Quincke khi tiêm kháng sinh nhóm beta-lactam" },
                    new MedicalHistoryItem { Type = MedicalHistoryTypes.Condition, Name = "Tăng huyết áp", IsCritical = false, Detail = "Huyết áp vô căn độ 1, đang uống thuốc duy trì Amlodipine" }
                ]
            },
            new MedicalHistoryRecord
            {
                PatientId = p2.PatientId, VisitId = v2.VisitId, RecordedByUserId = assistant1.UserId, CreatedAt = now.Date.AddHours(9).AddMinutes(22),
                Items =
                [
                    new MedicalHistoryItem { Type = MedicalHistoryTypes.Allergy, Name = "Aspirin", IsCritical = true, Detail = "Co thắt phế quản, khó thở" }
                ]
            }
        );

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
            }
        );
        await db.SaveChangesAsync(ct);

        // 2. Sơ đồ răng FDI (MOD_FDI)
        db.ToothConditions.AddRange(
            new ToothCondition { VisitId = v1.VisitId, ToothNumber = 36, Surface = "O", ConditionCode = "CARIES", Note = "Sâu men ngà nông xoang I mặt nhai", CreatedAt = now.Date.AddHours(8).AddMinutes(40) },
            new ToothCondition { VisitId = v1.VisitId, ToothNumber = 36, Surface = "O", ConditionCode = "RESTORED", Note = "Trám Composite quang trùng hợp hoàn thiện", CreatedAt = now.Date.AddHours(9).AddMinutes(0) },
            new ToothCondition { VisitId = v1.VisitId, ToothNumber = 48, Surface = null, ConditionCode = "IMPACTED", Note = "Răng khôn mọc lệch nghiêng gần 45 độ, đau khi giắt thức ăn", CreatedAt = now.Date.AddHours(8).AddMinutes(42) },
            new ToothCondition { VisitId = v2.VisitId, ToothNumber = 21, Surface = "M", ConditionCode = "RESTORED", Note = "Đang tạo hình xoang trám phục hồi góc cắn", CreatedAt = now.Date.AddHours(9).AddMinutes(30) }
        );
        await db.SaveChangesAsync(ct);

        // 3. Chỉ định dịch vụ theo răng (MOD_FDI)
        var services = await db.DentalServices.Include(s => s.Prices).ToDictionaryAsync(s => s.Code, s => s, ct);
        var dv003 = services["DV003"];
        var dv002 = services["DV002"];
        var p003 = dv003.Prices.OrderByDescending(p => p.EffectiveFrom).First();
        var p002 = dv002.Prices.OrderByDescending(p => p.EffectiveFrom).First();

        var vs1 = new VisitService { VisitId = v1.VisitId, ServiceId = dv003.DentalServiceId, ServicePriceId = p003.ServicePriceId, ServiceCode = dv003.Code, ServiceName = dv003.Name, ToothNumber = 36, Surface = "O", Quantity = 1, UnitPrice = p003.Amount, CreatedByUserId = dentist1.UserId, CreatedAt = now.Date.AddHours(8).AddMinutes(45) };
        var vs2 = new VisitService { VisitId = v1.VisitId, ServiceId = dv002.DentalServiceId, ServicePriceId = p002.ServicePriceId, ServiceCode = dv002.Code, ServiceName = dv002.Name, Quantity = 1, UnitPrice = p002.Amount, CreatedByUserId = dentist1.UserId, CreatedAt = now.Date.AddHours(8).AddMinutes(50) };
        var vs3 = new VisitService { VisitId = v2.VisitId, ServiceId = dv003.DentalServiceId, ServicePriceId = p003.ServicePriceId, ServiceCode = dv003.Code, ServiceName = dv003.Name, ToothNumber = 21, Surface = "M", Quantity = 1, UnitPrice = p003.Amount, CreatedByUserId = dentist1.UserId, CreatedAt = now.Date.AddHours(9).AddMinutes(35) };
        db.VisitServices.AddRange(vs1, vs2, vs3);
        await db.SaveChangesAsync(ct);

        // 4. Đơn thuốc, Hóa đơn & Kho (MOD_RX / MOD_BIL / MOD_INV)
        await SeedRxAndInvoiceAsync(db, v1, vs1, vs2, dentist1, receptionist1, assistant1, adminId, today, now, ct);
    }
}
