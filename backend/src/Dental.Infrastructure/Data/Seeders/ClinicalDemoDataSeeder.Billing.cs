using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

public static partial class ClinicalDemoDataSeeder
{
    private static async Task SeedRxAndInvoiceAsync(
        DentalDbContext db, Visit v1, Visit v3, VisitService vs1, VisitService vs2, VisitService vs4,
        User dentist1, User dentist2, User receptionist1, User assistant1, int adminId,
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
        var rx2 = new Prescription
        {
            VisitId = v3.VisitId, DoctorId = dentist2.UserId, PrescriptionCode = $"RX-{today.AddDays(-2):yyyyMMdd}-0001",
            Diagnosis = "Viêm tủy không hồi phục răng 46 (đã mở tủy)",
            DoctorAdvice = "Tránh nhai bên phải cho đến khi hàn ống tủy vĩnh viễn",
            RevisitDate = today.AddDays(3), IsCancelled = false,
            CreatedAt = now.Date.AddDays(-2).AddHours(10).AddMinutes(40), CreatedByUserId = dentist2.UserId, Version = 1,
            Items =
            [
                new PrescriptionItem
                {
                    MedicineId = medAmoxi!.Id, MedicineCode = medAmoxi.Code, MedicineName = medAmoxi.Name,
                    ActiveIngredient = medAmoxi.ActiveIngredient, Strength = "500mg", Unit = medAmoxi.Unit,
                    Quantity = 15, UnitPrice = 5000m, UsageInstructions = "Uống 1 viên x 3 lần/ngày sau ăn", TreatmentDays = 5,
                },
                new PrescriptionItem
                {
                    MedicineId = medIbu!.Id, MedicineCode = medIbu.Code, MedicineName = medIbu.Name,
                    ActiveIngredient = medIbu.ActiveIngredient, Strength = "400mg", Unit = medIbu.Unit,
                    Quantity = 10, UnitPrice = 3000m, UsageInstructions = "Uống 1 viên khi đau sau ăn", TreatmentDays = 3,
                }
            ]
        };
        db.Prescriptions.AddRange(rx1, rx2);
        await db.SaveChangesAsync(ct);

        var presetFix50 = await db.DiscountPresets.FirstOrDefaultAsync(p => p.Code == "FIX50", ct);
        var rxItems = rx1.Items.ToList();
        var rx2Items = rx2.Items.ToList();

        var invoice1 = new Invoice
        {
            VisitId = v1.VisitId, InvoiceCode = $"HD-{today:yyyyMMdd}-0001", Status = InvoiceStatus.PendingPayment,
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

        var rx2Total = rx2Items.Sum(x => x.UnitPrice * x.Quantity);
        var inv2Total = vs4.UnitPrice + rx2Total;
        var inv2Paid = Math.Min(inv2Total, 800000m);
        var invoice2 = new Invoice
        {
            VisitId = v3.VisitId, InvoiceCode = $"HD-{today.AddDays(-2):yyyyMMdd}-0001",
            Status = inv2Paid >= inv2Total ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid,
            TotalAmount = inv2Total, PaidAmount = inv2Paid, DiscountAmount = 0m,
            CreatedByUserId = receptionist1.UserId, CreatedAt = now.Date.AddDays(-2).AddHours(10).AddMinutes(52),
            Items =
            [
                new InvoiceItem { SourceVisitServiceId = vs4.Id, ItemType = "Service", Code = vs4.ServiceCode, Name = vs4.ServiceName, ToothNumber = 46, Surface = "O", Quantity = 1, UnitPrice = vs4.UnitPrice, TotalAmount = vs4.UnitPrice },
                new InvoiceItem { SourceVisitServiceId = 0, SourcePrescriptionItemId = rx2Items[0].Id, ItemType = "Medicine", Code = rx2Items[0].MedicineCode, Name = rx2Items[0].MedicineName, Quantity = rx2Items[0].Quantity, UnitPrice = rx2Items[0].UnitPrice, TotalAmount = rx2Items[0].UnitPrice * rx2Items[0].Quantity },
                new InvoiceItem { SourceVisitServiceId = 0, SourcePrescriptionItemId = rx2Items[1].Id, ItemType = "Medicine", Code = rx2Items[1].MedicineCode, Name = rx2Items[1].MedicineName, Quantity = rx2Items[1].Quantity, UnitPrice = rx2Items[1].UnitPrice, TotalAmount = rx2Items[1].UnitPrice * rx2Items[1].Quantity }
            ]
        };
        db.Invoices.AddRange(invoice1, invoice2);
        rx1.Invoice = invoice1;
        rx2.Invoice = invoice2;
        await db.SaveChangesAsync(ct);

        invoice1.Status = InvoiceStatus.Paid;
        invoice1.PaidAmount = 585000m;
        var paymentTx1 = new PaymentTransaction
        {
            InvoiceId = invoice1.Id, Amount = 585000m, AmountTendered = 600000m, ChangeAmount = 15000m,
            PaymentMethod = "Cash", CashierId = receptionist1.UserId, PaidAt = now.Date.AddHours(9).AddMinutes(18),
            RequestId = Guid.NewGuid(), TransactionType = "Receipt", PrintCount = 1, Note = "Thu tiền mặt đủ, đã in biên lai",
        };
        var paymentTx2 = new PaymentTransaction
        {
            InvoiceId = invoice2.Id, Amount = inv2Paid, AmountTendered = inv2Paid, ChangeAmount = 0m,
            PaymentMethod = "BankTransfer", TransactionReference = "VCB-DEMO-202611-001", Source = "ManualConfirm", BankReceivedAmount = inv2Paid,
            CashierId = receptionist1.UserId,
            PaidAt = now.Date.AddDays(-2).AddHours(10).AddMinutes(55),
            RequestId = Guid.NewGuid(), TransactionType = "Receipt", PrintCount = 1, Note = "Thu đợt 1 bằng chuyển khoản thủ công (trả góp theo tiến độ điều trị)",
        };
        db.PaymentTransactions.AddRange(paymentTx1, paymentTx2);
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

        db.AuditLogs.AddRange(
            new AuditLog { UserId = adminId, Action = AuditActions.Create, EntityType = "SystemInit", Detail = "{\"source\":\"demo_seed\"}", CreatedAt = now.AddDays(-7) },
            new AuditLog { UserId = receptionist1.UserId, Action = AuditActions.QueueCheckedIn, EntityType = "QueueEntry", Detail = "{\"queueNumber\":1}", CreatedAt = now.Date.AddHours(8).AddMinutes(25) },
            new AuditLog { UserId = receptionist1.UserId, Action = "PAYMENT_CONFIRMED", EntityType = "Invoice", EntityId = invoice1.Id, Detail = "{\"method\":\"Cash\",\"amount\":585000}", CreatedAt = now.Date.AddHours(9).AddMinutes(18) },
            new AuditLog { UserId = receptionist1.UserId, Action = "PAYMENT_CONFIRMED", EntityType = "Invoice", EntityId = invoice2.Id, Detail = "{\"method\":\"BankTransfer\",\"amount\":800000}", CreatedAt = now.Date.AddDays(-2).AddHours(10).AddMinutes(55) }
        );
        await db.SaveChangesAsync(ct);

        await SeedStockAsync(db, v1, assistant1, adminId, rx1, now, ct);
    }
}
