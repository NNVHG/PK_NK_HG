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
        var balances = await db.MedicineStockBalances.AsNoTracking().Where(b => ids.Contains(b.MedicineId)).ToDictionaryAsync(b => b.MedicineId, ct);
        var units = await db.Medicines.Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Unit, ct);
        var issues = await db.MedicineStockIssues.AsNoTracking().Where(i => i.PrescriptionId == rx.Id).ToArrayAsync(ct);
        var legacy = issues.Length == 0 && (rx.DispensedAt is not null || await db.PrescriptionDispenseEvents.AnyAsync(e => e.PrescriptionId == rx.Id, ct));
        var status = lines.Select(i => new PrescriptionStockLine(i.MedicineId, i.MedicineCode, i.MedicineName, i.Unit, i.Quantity,
            balances.GetValueOrDefault(i.MedicineId)?.OnHand ?? 0, units.GetValueOrDefault(i.MedicineId) == i.Unit)).ToArray();
        var matchingIssues = issues.Length == lines.Length && issues.All(i => lines.Any(l => l.Id == i.PrescriptionItemId && l.MedicineId == i.MedicineId && l.Quantity == i.Quantity && l.Unit == i.Unit));
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
        var balances = await db.MedicineStockBalances.Where(b => ids.Contains(b.MedicineId)).ToDictionaryAsync(b => b.MedicineId, ct);
        if (lines.Any(i => !balances.TryGetValue(i.MedicineId, out var b) || b.OnHand < i.Quantity || b.Version == long.MaxValue)) return Result.Failure(Insufficient);
        foreach (var line in lines)
        {
            var balance = balances[line.MedicineId]; balance.OnHand -= line.Quantity; balance.Version++; balance.UpdatedAt = clock.UtcNow;
            db.MedicineStockIssues.Add(new MedicineStockIssue { PrescriptionId = rx.Id, PrescriptionItemId = line.Id, MedicineId = line.MedicineId,
                MedicineCode = line.MedicineCode, MedicineName = line.MedicineName, Unit = line.Unit, Quantity = line.Quantity, DispenseEvent = record,
                IssuedAt = clock.UtcNow, IssuedByUserId = actor });
        }
        return Result.Success();
    }
}
