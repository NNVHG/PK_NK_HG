using System.Data;
using Dental.Application.Common;
using Dental.Application.Features.MOD_RX;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dental.Infrastructure.Services;

// Called by handover inside its existing transaction. Metadata undo never calls IssueAsync.
internal sealed class MedicineStockLedger(DentalDbContext db, VietnamClock clock)
{
    public static readonly Error Insufficient = new("RX_STOCK_INSUFFICIENT", "Không đủ tồn thuốc hoặc đơn vị không khớp. Tải lại, đối chiếu và nhập thuốc thực tế trước khi giao.");

    public async Task<PrescriptionStockStatus> InspectAsync(Prescription rx, CancellationToken ct)
    {
        var lines = await db.PrescriptionItems.AsNoTracking().Where(i => i.PrescriptionId == rx.Id).OrderBy(i => i.Id).ToArrayAsync(ct);
        var ids = lines.Select(i => i.MedicineId).Distinct().ToArray();
        var meds = await db.Medicines.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        var issues = await db.InventoryTransactionLines.AsNoTracking()
            .Where(l => l.Transaction.ItemDomain == InventoryItemDomains.Medicine &&
                        l.Transaction.TransactionType == InventoryTransactionTypes.Issue &&
                        l.Transaction.PrescriptionId == rx.Id)
            .ToArrayAsync(ct);
        var legacy = issues.Length == 0 && (rx.DispensedAt is not null || rx.GetDispenseHistory().Count > 0);
        var status = lines.Select(i =>
        {
            var med = meds.GetValueOrDefault(i.MedicineId);
            return new PrescriptionStockLine(i.MedicineId, i.MedicineCode, i.MedicineName, i.Unit, i.Quantity,
                med?.OnHand ?? 0, med?.Unit == i.Unit);
        }).ToArray();
        var matchingIssues = issues.Length == lines.Length &&
            issues.All(i => lines.Any(l => l.Id == i.PrescriptionItemId && l.MedicineId == i.ItemId && l.Quantity == i.Quantity && l.Unit == i.Unit));
        var needs = !legacy && issues.Length == 0;
        return new(needs, legacy, legacy || (issues.Length > 0 ? matchingIssues : lines.Length > 0 && ids.Length == lines.Length && status.All(l => l.UnitMatches && l.Quantity > 0 && l.OnHand >= l.Quantity)), status);
    }

    public async Task<Result> IssueAsync(Prescription rx, PrescriptionDispenseEvent record, int actor, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Medicine issue requires the handover Serializable transaction.");
        var status = await InspectAsync(rx, ct);
        if (!status.CanFulfill) return Result.Failure(Insufficient);
        if (!status.NeedsDeduction) return Result.Success();
        var lines = await db.PrescriptionItems.AsNoTracking().Where(i => i.PrescriptionId == rx.Id).OrderBy(i => i.MedicineId).ToArrayAsync(ct);
        var ids = lines.Select(i => i.MedicineId).ToArray();
        var meds = await db.Medicines.Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        if (lines.Any(i => !meds.TryGetValue(i.MedicineId, out var m) || m.OnHand < i.Quantity || m.StockVersion == long.MaxValue))
            return Result.Failure(Insufficient);

        var txRow = new InventoryTransaction
        {
            ItemDomain = InventoryItemDomains.Medicine,
            TransactionType = InventoryTransactionTypes.Issue,
            PrescriptionId = rx.Id,
            DispenseEventId = record.Id,
            ConfirmedAt = clock.UtcNow,
            ConfirmedByUserId = actor,
        };
        foreach (var line in lines)
        {
            var med = meds[line.MedicineId];
            med.OnHand -= line.Quantity;
            med.StockVersion++;
            med.StockUpdatedAt = clock.UtcNow;
            txRow.Lines.Add(new InventoryTransactionLine
            {
                ItemId = line.MedicineId,
                PrescriptionItemId = line.Id,
                ItemCode = line.MedicineCode,
                ItemName = line.MedicineName,
                Unit = line.Unit,
                Quantity = line.Quantity,
            });
        }
        db.InventoryTransactions.Add(txRow);
        return Result.Success();
    }
}
