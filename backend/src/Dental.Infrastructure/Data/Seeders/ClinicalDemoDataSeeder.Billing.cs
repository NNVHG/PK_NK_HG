using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

public static partial class ClinicalDemoDataSeeder
{
    private static async Task SeedRxAndInvoiceAsync(
        DentalDbContext db, Visit v1, VisitService vs1, VisitService vs2,
        User dentist1, User receptionist1, User assistant1, int adminId,
        DateOnly today, DateTime now, CancellationToken ct)
    {
        var medParacetamol = await db.Medicines.FirstOrDefaultAsync(m => m.Code == "MED011", ct);
        var medChloro = await db.Medicines.FirstOrDefaultAsync(m => m.Code == "MED020", ct);
        var medAmoxi = await db.Medicines.FirstOrDefaultAsync(m => m.Code == "MED001", ct);
        var medIbu = await db.Medicines.FirstOrDefaultAsync(m => m.Code == "MED012", ct);
        var medLido = await db.Medicines.FirstOrDefaultAsync(m => m.Code == "MED024", ct);

        if (medParacetamol != null) { medParacetamol.UnitPrice = 2000m; medParacetamol.IsActive = true; }
        if (medChloro != null) { medChloro.UnitPrice = 65000m; medChloro.IsActive = true; }
        if (medAmoxi != null) { medAmoxi.UnitPrice = 5000m; medAmoxi.IsActive = true; }
        if (medIbu != null) { medIbu.UnitPrice = 3000m; medIbu.IsActive = true; }
        if (medLido != null) { medLido.UnitPrice = 25000m; medLido.IsActive = true; }
        await db.SaveChangesAsync(ct);

        var rx1 = new Prescription
        {
            VisitId = v1.VisitId, DoctorId = dentist1.UserId, PrescriptionCode = $"RX-{today:yyyyMMdd}-0001",
            Diagnosis = "Viêm nướu sau lấy vôi răng, ê buốt nhẹ sau trám R36",
            DoctorAdvice = "Súc miệng đều đặn, không nhai đồ cứng tại răng 36 trong 24 giờ đầu",
            RevisitDate = today.AddDays(7), IsCancelled = false,
            CreatedAt = now.Date.AddHours(9).AddMinutes(10), CreatedByUserId = dentist1.UserId, Version = 1,
            Items =
            [
                new PrescriptionItem
                {
                    MedicineId = medParacetamol!.Id, MedicineCode = medParacetamol.Code, MedicineName = medParacetamol.Name,
                    ActiveIngredient = medParacetamol.ActiveIngredient, Strength = "500mg", Unit = medParacetamol.Unit,
                    Quantity = 10, UnitPrice = 2000m, UsageInstructions = "Uống 1 viên khi đau nhức, cách tối thiểu 4-6 tiếng", TreatmentDays = 3,
                },
                new PrescriptionItem
                {
                    MedicineId = medChloro!.Id, MedicineCode = medChloro.Code, MedicineName = medChloro.Name,
                    ActiveIngredient = medChloro.ActiveIngredient, Strength = "0.12%", Unit = medChloro.Unit,
                    Quantity = 1, UnitPrice = 65000m, UsageInstructions = "Súc miệng 15ml ngày 2 lần sáng/tối sau ăn", TreatmentDays = 7,
                }
            ]
        };
        db.Prescriptions.Add(rx1);
        await db.SaveChangesAsync(ct);

        var presetFix50 = await db.DiscountPresets.FirstOrDefaultAsync(p => p.Code == "FIX50", ct);
        var rxItems = rx1.Items.ToList();

        var invoice1 = new Invoice
        {
            VisitId = v1.VisitId, InvoiceCode = $"HD-{today:yyyyMMdd}-0001", Status = InvoiceStatus.Draft,
            TotalAmount = 635000m, PaidAmount = 0m, DiscountAmount = 50000m, DiscountType = "FixedAmount",
            DiscountValue = 50000m, DiscountPresetId = presetFix50?.Id, DiscountReason = "Áp dụng mẫu ưu đãi FIX50 cho khách hàng",
            CreatedByUserId = receptionist1.UserId, CreatedAt = now.Date.AddHours(9).AddMinutes(16),
            Items =
            [
                new InvoiceItem { SourceVisitServiceId = vs1.Id, ItemType = "Service", Code = vs1.ServiceCode, Name = vs1.ServiceName, ToothNumber = 36, Surface = "O", Quantity = 1, UnitPrice = 350000m, TotalAmount = 350000m },
                new InvoiceItem { SourceVisitServiceId = vs2.Id, ItemType = "Service", Code = vs2.ServiceCode, Name = vs2.ServiceName, Quantity = 1, UnitPrice = 200000m, TotalAmount = 200000m },
                new InvoiceItem { SourceVisitServiceId = 0, SourcePrescriptionItemId = rxItems[0].Id, ItemType = "Medicine", Code = rxItems[0].MedicineCode, Name = rxItems[0].MedicineName, Quantity = 10, UnitPrice = 2000m, TotalAmount = 20000m },
                new InvoiceItem { SourceVisitServiceId = 0, SourcePrescriptionItemId = rxItems[1].Id, ItemType = "Medicine", Code = rxItems[1].MedicineCode, Name = rxItems[1].MedicineName, Quantity = 1, UnitPrice = 65000m, TotalAmount = 65000m }
            ]
        };
        db.Invoices.Add(invoice1);
        rx1.Invoice = invoice1;
        await db.SaveChangesAsync(ct);

        invoice1.Status = InvoiceStatus.Paid;
        invoice1.PaidAmount = 585000m;
        var paymentTx1 = new PaymentTransaction
        {
            InvoiceId = invoice1.Id, Amount = 585000m, AmountTendered = 600000m, ChangeAmount = 15000m,
            PaymentMethod = "Cash", CashierId = receptionist1.UserId, PaidAt = now.Date.AddHours(9).AddMinutes(18),
            RequestId = Guid.NewGuid(), TransactionType = "Receipt", PrintCount = 1, Note = "Thu tiền mặt đủ, đã in biên lai",
        };
        db.PaymentTransactions.Add(paymentTx1);
        await db.SaveChangesAsync(ct);

        rx1.DispensedAt = now.Date.AddHours(9).AddMinutes(20);
        rx1.DispensedBy = assistant1.UserId;
        rx1.ReceivedByProxy = false;
        rx1.DispenseNote = "Đã dặn dò liều lượng chi tiết";
        rx1.Version = 2;
        rx1.UpdatedAt = now.Date.AddHours(9).AddMinutes(20);
        db.PrescriptionDispenseEvents.Add(new PrescriptionDispenseEvent
        {
            PrescriptionId = rx1.Id, PrescriptionVersion = 2, Action = "Dispense",
            PerformedAt = now.Date.AddHours(9).AddMinutes(20), PerformedBy = assistant1.UserId, ReceivedByProxy = false,
        });
        await db.SavePrescriptionHandoverAsync(ct);

        await SeedStockAsync(db, v1, assistant1, adminId, rx1, now, ct);
    }
}
