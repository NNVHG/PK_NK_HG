using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_INV;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class StockReceiptRepository(DentalDbContext db) : IStockReceiptRepository
{
    private Task<bool> Actor(int id, string role, CancellationToken ct) => db.Users.AnyAsync(x => x.UserId == id && x.IsActive && x.Role.RoleCode == role, ct);
    private static readonly Error Changed = new("INV_409", "Dữ liệu kho/danh mục đã thay đổi hoặc mã gửi phiếu đã được dùng. Tải lại để kiểm tra; gửi lại phiếu chưa rõ kết quả với cùng mã.");
    private static StockReceiptDto Dto(StockReceipt r, string name) => new(r.Id, $"PN{r.Id:D6}", r.SupplierName, r.Notes, r.ConfirmedAt,
        r.ConfirmedByUserId, name, r.Lines.OrderBy(x => x.MedicalSupplyId).Select(x => new StockReceiptLineDto(x.MedicalSupplyId, x.SupplyCode, x.SupplyName, x.Unit, x.Quantity)).ToArray());
    private async Task<StockReceiptDto> DtoAsync(StockReceipt r, CancellationToken ct) => Dto(r, await db.Users.Where(x => x.UserId == r.ConfirmedByUserId).Select(x => x.FullName).SingleAsync(ct));
    public async Task<Result<PagedResult<StockItemDto>>> StockAsync(string keyword, int page, int actor, string role, CancellationToken ct)
    {
        if (!StockReceiptService.CanRead(role) || !await Actor(actor, role, ct)) return Result<PagedResult<StockItemDto>>.Failure(Error.Forbidden);
        var key = DiagnosisSearch.Normalize(keyword);
        var query = db.MedicalSupplies.AsNoTracking().Where(x => (role != Dental.Domain.Constants.RoleCodes.Dentist || x.IsActive) && (key == "" || x.SearchText.Contains(key)));
        var count = await query.CountAsync(ct);
        var items = await (from s in query
            join b in db.StockBalances on s.Id equals b.MedicalSupplyId into balances
            from b in balances.DefaultIfEmpty()
            orderby s.Code, s.Id
            select new StockItemDto(s.Id, s.Code, s.Name, s.Unit, b == null ? 0 : b.OnHand, s.MinimumStockThreshold, s.IsActive, s.Version))
            .Skip((page - 1) * 20).Take(20).ToArrayAsync(ct);
        return Result<PagedResult<StockItemDto>>.Success(PagedResult<StockItemDto>.Create(items, count, page, 20));
    }
    public async Task<Result<StockReceiptPage>> ListAsync(int beforeId, int actor, string role, CancellationToken ct)
    {
        if (!StockReceiptService.CanWrite(role) || !await Actor(actor, role, ct)) return Result<StockReceiptPage>.Failure(Error.Forbidden);
        var rows = await db.StockReceipts.AsNoTracking().Include(x => x.Lines).Where(x => beforeId == 0 || x.Id < beforeId).OrderByDescending(x => x.Id).Take(21).ToArrayAsync(ct);
        var ids = rows.Take(20).Select(x => x.ConfirmedByUserId).Distinct().ToArray();
        var names = await db.Users.Where(x => ids.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, x => x.FullName, ct);
        return Result<StockReceiptPage>.Success(new(rows.Take(20).Select(x => Dto(x, names[x.ConfirmedByUserId])).ToArray(), rows.Length > 20));
    }
    public async Task<Result<StockReceiptDto>> GetAsync(int id, int actor, string role, CancellationToken ct)
    {
        if (!StockReceiptService.CanWrite(role) || !await Actor(actor, role, ct)) return Result<StockReceiptDto>.Failure(Error.Forbidden);
        var receipt = await db.StockReceipts.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
        return receipt is null ? Result<StockReceiptDto>.Failure(Error.NotFound) : Result<StockReceiptDto>.Success(await DtoAsync(receipt, ct));
    }
    public async Task<Result<StockReceiptSaved>> ConfirmAsync(StockReceiptRequest request, int actor, string role, CancellationToken ct)
    {
        if (!StockReceiptService.CanWrite(role)) return Result<StockReceiptSaved>.Failure(Error.Forbidden);
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new { request.SupplierName, request.Notes, Lines = request.Lines.OrderBy(x => x.MedicalSupplyId) });
        var hash = Convert.ToHexString(SHA256.HashData(canonical));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (!await Actor(actor, role, ct)) return Result<StockReceiptSaved>.Failure(Error.Forbidden);
            var prior = await db.StockReceipts.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.RequestId == request.RequestId, ct);
            if (prior is not null)
            {
                if (prior.ConfirmedByUserId != actor || prior.PayloadHash != hash) return Result<StockReceiptSaved>.Failure(Changed);
                return Result<StockReceiptSaved>.Success(new(await DtoAsync(prior, ct), true));
            }
            var ids = request.Lines.Select(x => x.MedicalSupplyId).ToArray();
            var supplies = await db.MedicalSupplies.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            var balances = await db.StockBalances.Where(x => ids.Contains(x.MedicalSupplyId)).ToDictionaryAsync(x => x.MedicalSupplyId, ct);
            var now = DateTime.UtcNow;
            var receipt = new StockReceipt { RequestId = request.RequestId, PayloadHash = hash, SupplierName = request.SupplierName, Notes = request.Notes, ConfirmedAt = now, ConfirmedByUserId = actor };
            foreach (var line in request.Lines.OrderBy(x => x.MedicalSupplyId))
            {
                if (!supplies.TryGetValue(line.MedicalSupplyId, out var supply) || !supply.IsActive || supply.Version != line.ExpectedSupplyVersion)
                    return Result<StockReceiptSaved>.Failure(new Error("INV_STALE", "Vật tư đã thay đổi hoặc ngừng dùng. Tải lại và chọn lại các dòng vật tư trước khi xác nhận."));
                if (!balances.TryGetValue(supply.Id, out var balance))
                {
                    balance = new StockBalance { MedicalSupplyId = supply.Id };
                    db.StockBalances.Add(balance);
                }
                if (line.Quantity > int.MaxValue - balance.OnHand || balance.Version == long.MaxValue) return Result<StockReceiptSaved>.Failure(new Error("INV_002", "Số tồn vượt giới hạn số nguyên. Kiểm tra lại số lượng và đơn vị."));
                balance.OnHand += line.Quantity;
                balance.Version++;
                balance.UpdatedAt = now;
                receipt.Lines.Add(new StockReceiptLine { MedicalSupplyId = supply.Id, Quantity = line.Quantity, SupplyCode = supply.Code, SupplyName = supply.Name, Unit = supply.Unit });
            }
            db.StockReceipts.Add(receipt);
            await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog { UserId = actor, Action = "MOD_INV_RECEIPT_CONFIRMED", EntityType = "StockReceipt", EntityId = receipt.Id, CreatedAt = now,
                Detail = JsonSerializer.Serialize(new { ReceiptId = receipt.Id, Lines = receipt.Lines.Select(x => new { x.MedicalSupplyId, x.Quantity }) }) });
            await db.SaveChangesAsync(ct);
            var dto = await DtoAsync(receipt, ct);
            await tx.CommitAsync(ct);
            committed = true;
            return Result<StockReceiptSaved>.Success(new(dto, false));
        }
        catch (Exception e) when (Conflict(e)) { return Result<StockReceiptSaved>.Failure(Changed); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static bool Conflict(Exception error)
    {
        for (Exception? e = error; e is not null; e = e.InnerException)
            if (e is DbUpdateConcurrencyException || e is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
