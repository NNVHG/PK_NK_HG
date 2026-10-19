using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_RX;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class MedicineStockRepository(DentalDbContext db) : IMedicineStockRepository
{
    private static readonly Error Changed = new("MED_STOCK_CONFLICT", "Tồn hoặc danh mục vừa thay đổi. Tải lại để đối chiếu; phiếu chưa rõ kết quả phải gửi lại cùng mã.");
    private Task<bool> Admin(int actor, CancellationToken ct) => db.Users.AnyAsync(u => u.UserId == actor && u.IsActive && u.Role.RoleCode == RoleCodes.Admin, ct);
    private static MedicineStockReceiptDto Dto(MedicineStockReceipt r, string actorName) => new(r.Id, $"NTH{r.Id:D6}", r.MedicineId, r.MedicineCode, r.MedicineName, r.Unit, r.Quantity, r.SupplierName, r.Notes, r.ConfirmedAt, actorName);
    private async Task<MedicineStockReceiptDto> DtoAsync(MedicineStockReceipt r, CancellationToken ct)
        => Dto(r, await db.Users.Where(u => u.UserId == r.ConfirmedByUserId).Select(u => u.FullName).SingleAsync(ct));
    public async Task<Result<MedicineStockDetail>> ReadAsync(int id, int beforeId, int actor, CancellationToken ct)
    {
        if (!await Admin(actor, ct)) return Result<MedicineStockDetail>.Failure(Error.Forbidden);
        var medicine = await db.Medicines.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);
        if (medicine is null) return Result<MedicineStockDetail>.Failure(Error.NotFound);
        var onHand = await db.MedicineStockBalances.Where(b => b.MedicineId == id).Select(b => (int?)b.OnHand).SingleOrDefaultAsync(ct) ?? 0;
        var receipts = await db.MedicineStockReceipts.AsNoTracking().Where(r => r.MedicineId == id && (beforeId == 0 || r.Id < beforeId)).OrderByDescending(r => r.Id).Take(21).ToArrayAsync(ct);
        var actors = receipts.Take(20).Select(r => r.ConfirmedByUserId).Distinct().ToArray();
        var names = await db.Users.Where(u => actors.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);
        return Result<MedicineStockDetail>.Success(new(id, medicine.Code, medicine.Name, medicine.Unit, medicine.Version, medicine.IsActive, onHand,
            receipts.Take(20).Select(r => Dto(r, names[r.ConfirmedByUserId])).ToArray(), receipts.Length > 20));
    }
    public async Task<Result<MedicineStockReceiptSaved>> ReceiveAsync(int id, MedicineStockReceiptRequest request, int actor, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { MedicineId = id, request.Quantity, request.ExpectedMedicineVersion, request.SupplierName, request.Notes })));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var committed = false;
        try
        {
            if (!await Admin(actor, ct)) return Result<MedicineStockReceiptSaved>.Failure(Error.Forbidden);
            var prior = await db.MedicineStockReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.RequestId == request.RequestId, ct);
            if (prior is not null)
                return prior.ConfirmedByUserId == actor && prior.PayloadHash == hash
                    ? Result<MedicineStockReceiptSaved>.Success(new(await DtoAsync(prior, ct), true)) : Result<MedicineStockReceiptSaved>.Failure(Changed);
            var medicine = await db.Medicines.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);
            if (medicine is null) return Result<MedicineStockReceiptSaved>.Failure(Error.NotFound);
            if (!medicine.IsActive || medicine.Version != request.ExpectedMedicineVersion)
                return Result<MedicineStockReceiptSaved>.Failure("MED_STOCK_STALE", "Thuốc đã thay đổi hoặc ngừng dùng. Tải lại và chọn thuốc trước khi lập phiếu mới.");
            var balance = await db.MedicineStockBalances.SingleOrDefaultAsync(b => b.MedicineId == id, ct);
            if (balance is null) { balance = new MedicineStockBalance { MedicineId = id }; db.MedicineStockBalances.Add(balance); }
            if (request.Quantity > int.MaxValue - balance.OnHand || balance.Version == long.MaxValue)
                return Result<MedicineStockReceiptSaved>.Failure("MED_STOCK_LIMIT", "Tồn vượt giới hạn số nguyên. Kiểm tra đơn vị và số lượng.");
            var now = DateTime.UtcNow; balance.OnHand += request.Quantity; balance.Version++; balance.UpdatedAt = now;
            var receipt = new MedicineStockReceipt { RequestId = request.RequestId, PayloadHash = hash, MedicineId = id, MedicineCode = medicine.Code, MedicineName = medicine.Name, Unit = medicine.Unit,
                Quantity = request.Quantity, SupplierName = request.SupplierName, Notes = request.Notes, ConfirmedAt = now, ConfirmedByUserId = actor };
            db.MedicineStockReceipts.Add(receipt); await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog { UserId = actor, Action = "MOD_RX_STOCK_RECEIVED", EntityType = "MedicineStockReceipt", EntityId = receipt.Id, CreatedAt = now,
                Detail = JsonSerializer.Serialize(new { receipt.MedicineId, receipt.Quantity, balance.Version }) });
            await db.SaveChangesAsync(ct); var result = new MedicineStockReceiptSaved(await DtoAsync(receipt, ct), false);
            await tx.CommitAsync(ct); committed = true; return Result<MedicineStockReceiptSaved>.Success(result);
        }
        catch (Exception e) when (IsConflict(e)) { return Result<MedicineStockReceiptSaved>.Failure(Changed); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    public async Task<Result<MedicineAvailability[]>> AvailabilityAsync(int visitId, int[] ids, int actor, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.UserId == actor && u.IsActive && u.Role.RoleCode == RoleCodes.Dentist, ct)) return Result<MedicineAvailability[]>.Failure(Error.Forbidden);
        if (!await db.Visits.AnyAsync(v => v.VisitId == visitId && v.DentistId == actor && !v.IsLocked &&
            (v.Status == VisitStatuses.InProgress || v.Status == VisitStatuses.Correcting), ct)) return Result<MedicineAvailability[]>.Failure(Error.NotFound);
        if (await db.Invoices.AnyAsync(i => i.VisitId == visitId && (i.PaidAmount != 0 || i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.PartiallyRefunded || i.Status == InvoiceStatus.Refunded), ct) ||
            await db.PaymentTransactions.AnyAsync(t => t.Invoice.VisitId == visitId, ct)) return Result<MedicineAvailability[]>.Failure("RX_LOCKED", "Hồ sơ đã thu tiền; không còn luồng kê đơn mở.");
        var items = await (from m in db.Medicines.AsNoTracking().Where(m => ids.Contains(m.Id))
            join b in db.MedicineStockBalances on m.Id equals b.MedicineId into balances from b in balances.DefaultIfEmpty()
            orderby m.Id select new MedicineAvailability(m.Id, m.Code, m.Name, m.Unit, b == null ? 0 : b.OnHand, m.IsActive)).ToArrayAsync(ct);
        return Result<MedicineAvailability[]>.Success(items);
    }
    private static bool IsConflict(Exception error)
    {
        for (Exception? e = error; e is not null; e = e.InnerException)
            if (e is DbUpdateConcurrencyException || e is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
