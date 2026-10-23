using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

public static partial class ClinicalDemoDataSeeder
{
    private static async Task SeedStockAsync(
        DentalDbContext db, Visit v1, User assistant1, int adminId,
        Prescription rx1, DateTime now, CancellationToken ct)
    {
        var supplies = await db.MedicalSupplies.OrderBy(s => s.Code).ToListAsync(ct);
        if (supplies.Count > 0 && !await db.StockReceipts.AnyAsync(ct))
        {
            var supplyReceipt = new StockReceipt
            {
                RequestId = Guid.NewGuid(),
                PayloadHash = "SEED_INITIAL_MEDICAL_SUPPLIES_RECEIPT_HASH_2026",
                SupplierName = "Công ty Thiết bị Y tế Hoàng Minh",
                Notes = "Nhập kho ban đầu các vật tư tiêu hao phòng khám",
                ConfirmedByUserId = assistant1.UserId,
                ConfirmedAt = now.AddDays(-7),
                Lines = supplies.Select(s => new StockReceiptLine
                {
                    MedicalSupplyId = s.Id,
                    SupplyCode = s.Code,
                    SupplyName = s.Name,
                    Unit = s.Unit,
                    // Cố ý nhập số lượng 3 cho VT003 để tồn kho < ngưỡng tối thiểu 5 (thử nghiệm cảnh báo tồn tối thiểu F_INV_03)
                    Quantity = s.Code == "VT003" ? 3 : (s.Code is "VT001" or "VT002" ? 50 : 25)
                }).ToList()
            };
            db.StockReceipts.Add(supplyReceipt);
            await db.SaveChangesAsync(ct);

            var vt001 = supplies.First(s => s.Code == "VT001");
            var vt004 = supplies.First(s => s.Code == "VT004");
            var vt005 = supplies.First(s => s.Code == "VT005");

            var stockIssue = new StockIssue
            {
                VisitId = v1.VisitId,
                RequestId = Guid.NewGuid(),
                PayloadHash = "SEED_VISIT1_STOCK_ISSUE_HASH_2026",
                Notes = "Tiêu hao vật tư ca trám răng Composite và cạo vôi răng",
                ConfirmedByUserId = assistant1.UserId,
                ConfirmedAt = now.Date.AddHours(9).AddMinutes(20),
                Lines =
                [
                    new StockIssueLine { MedicalSupplyId = vt001.Id, SupplyCode = vt001.Code, SupplyName = vt001.Name, Unit = vt001.Unit, Quantity = 2 },
                    new StockIssueLine { MedicalSupplyId = vt004.Id, SupplyCode = vt004.Code, SupplyName = vt004.Name, Unit = vt004.Unit, Quantity = 1 },
                    new StockIssueLine { MedicalSupplyId = vt005.Id, SupplyCode = vt005.Code, SupplyName = vt005.Name, Unit = vt005.Unit, Quantity = 1 }
                ]
            };
            db.StockIssues.Add(stockIssue);
            await db.SaveChangesAsync(ct);

            foreach (var s in supplies)
            {
                var receivedQty = supplyReceipt.Lines.First(l => l.MedicalSupplyId == s.Id).Quantity;
                var issuedQty = stockIssue.Lines.FirstOrDefault(l => l.MedicalSupplyId == s.Id)?.Quantity ?? 0;
                db.StockBalances.Add(new StockBalance
                {
                    MedicalSupplyId = s.Id,
                    OnHand = receivedQty - issuedQty,
                    Version = 1,
                    UpdatedAt = now,
                });
            }
            await db.SaveChangesAsync(ct);
        }

        if (!await db.MedicineStockReceipts.AnyAsync(ct))
        {
            (string MedCode, int Qty, string Supplier)[] medReceipts =
            [
                ("MED011", 500, "Công ty Dược phẩm Dược Hậu Giang"),
                ("MED020", 50,  "Công ty TNHH Dược phẩm Đông Á"),
                ("MED001", 300, "Công ty Dược phẩm Imexpharm"),
                ("MED012", 200, "Công ty Dược phẩm Sanofi Việt Nam"),
                ("MED024", 100, "Công ty Cổ phần Thiết bị Y tế Việt Hàn"),
            ];

            foreach (var (medCode, qty, supplier) in medReceipts)
            {
                var med = await db.Medicines.FirstOrDefaultAsync(m => m.Code == medCode, ct);
                if (med == null) continue;

                db.MedicineStockReceipts.Add(new MedicineStockReceipt
                {
                    RequestId = Guid.NewGuid(),
                    PayloadHash = $"SEED_MED_RECEIPT_HASH_{medCode}",
                    MedicineId = med.Id,
                    MedicineCode = med.Code,
                    MedicineName = med.Name,
                    Unit = med.Unit,
                    Quantity = qty,
                    SupplierName = supplier,
                    Notes = "Nhập tồn đầu kỳ cho phòng khám",
                    ConfirmedByUserId = adminId,
                    ConfirmedAt = now.AddDays(-7),
                });

                var dispensedQty = rx1.Items.FirstOrDefault(i => i.MedicineId == med.Id)?.Quantity ?? 0;
                db.MedicineStockBalances.Add(new MedicineStockBalance
                {
                    MedicineId = med.Id,
                    OnHand = qty - dispensedQty,
                    Version = 1,
                    UpdatedAt = now,
                });
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
