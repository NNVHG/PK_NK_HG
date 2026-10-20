using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_INV;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Dental.Infrastructure.Repositories;

public sealed class StockIssueRepository(DentalDbContext db) : IStockIssueRepository
{
    private async Task<bool> Actor(int id, string role, CancellationToken ct) => StockReceiptService.CanWrite(role) && await db.Users.AnyAsync(x => x.UserId == id && x.IsActive && x.Role.RoleCode == role, ct);
    private static readonly Error ConflictError = new("INV_409", "Dữ liệu kho vừa thay đổi hoặc mã gửi phiếu đã được dùng. Gửi lại đúng phiếu với mã cũ để xác định kết quả.");
    private static StockIssueDto Dto(StockIssue x, string name) => new(x.Id, $"PX{x.Id:D6}", x.VisitId, x.Notes, x.ConfirmedAt, x.ConfirmedByUserId, name,
        x.Lines.OrderBy(l => l.MedicalSupplyId).Select(l => new StockIssueLineDto(l.MedicalSupplyId, l.SupplyCode, l.SupplyName, l.Unit, l.Quantity)).ToArray());
    private async Task<StockIssueDto> DtoAsync(StockIssue x, CancellationToken ct) => Dto(x, await db.Users.Where(u => u.UserId == x.ConfirmedByUserId).Select(u => u.FullName).SingleAsync(ct));
    public async Task<Result<StockIssueTargets>> TargetsAsync(int? visitId, int beforeId, int actor, string role, CancellationToken ct)
    {
        if (!await Actor(actor, role, ct)) return Result<StockIssueTargets>.Failure(Error.Forbidden);
        var rows = await db.Visits.AsNoTracking().Where(x => x.Status == VisitStatuses.Completed && x.Patient.IsActive && (visitId == null || x.VisitId == visitId) && (beforeId == 0 || x.VisitId < beforeId))
            .OrderByDescending(x => x.VisitId).Take(21).Select(x => new { x.VisitId, x.PatientId, x.Patient.PatientNumber, x.Patient.FullName, x.EndedAt }).ToArrayAsync(ct);
        return Result<StockIssueTargets>.Success(new(rows.Take(20).Select(x => new StockIssueTarget(x.VisitId, x.PatientId, $"BN{x.PatientNumber:D6}", x.FullName, x.EndedAt)).ToArray(), rows.Length > 20));
    }
    public async Task<Result<StockIssuePage>> ListAsync(int visitId, int beforeId, int actor, string role, CancellationToken ct)
    {
        if (!await Actor(actor, role, ct)) return Result<StockIssuePage>.Failure(Error.Forbidden);
        if (!await db.Visits.AnyAsync(x => x.VisitId == visitId, ct)) return Result<StockIssuePage>.Failure(Error.NotFound);
        var rows = await db.StockIssues.AsNoTracking().Include(x => x.Lines).Where(x => x.VisitId == visitId && (beforeId == 0 || x.Id < beforeId)).OrderByDescending(x => x.Id).Take(21).ToArrayAsync(ct);
        var ids = rows.Take(20).Select(x => x.ConfirmedByUserId).Distinct().ToArray();
        var names = await db.Users.Where(x => ids.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, x => x.FullName, ct);
        return Result<StockIssuePage>.Success(new(rows.Take(20).Select(x => Dto(x, names[x.ConfirmedByUserId])).ToArray(), rows.Length > 20));
    }
    public async Task<Result<StockIssueSaved>> ConfirmAsync(StockIssueRequest request, int actor, string role, CancellationToken ct)
    {
        if (!StockReceiptService.CanWrite(role)) return Result<StockIssueSaved>.Failure(Error.Forbidden);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.VisitId, request.Notes, Lines = request.Lines.OrderBy(x => x.MedicalSupplyId) })));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (!await Actor(actor, role, ct)) return Result<StockIssueSaved>.Failure(Error.Forbidden);
            var prior = await db.StockIssues.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.RequestId == request.RequestId, ct);
            if (prior is not null)
            {
                if (prior.ConfirmedByUserId != actor || prior.PayloadHash != hash) return Result<StockIssueSaved>.Failure(ConflictError);
                return Result<StockIssueSaved>.Success(new(await DtoAsync(prior, ct), true));
            }
            if (!await db.Visits.AnyAsync(x => x.VisitId == request.VisitId && x.Status == VisitStatuses.Completed && x.Patient.IsActive, ct))
                return Result<StockIssueSaved>.Failure(new Error("INV_VISIT", "Chỉ xác nhận tiêu hao cho ca đã hoàn tất có hồ sơ bệnh nhân đang hoạt động. Tải lại danh sách ca."));
            var ids = request.Lines.Select(x => x.MedicalSupplyId).ToArray();
            var supplies = await db.MedicalSupplies.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            var balances = await db.StockBalances.Where(x => ids.Contains(x.MedicalSupplyId)).ToDictionaryAsync(x => x.MedicalSupplyId, ct);
            var now = DateTime.UtcNow;
            var issue = new StockIssue { VisitId = request.VisitId, RequestId = request.RequestId, PayloadHash = hash, Notes = request.Notes, ConfirmedByUserId = actor, ConfirmedAt = now };
            foreach (var line in request.Lines.OrderBy(x => x.MedicalSupplyId))
            {
                if (!supplies.TryGetValue(line.MedicalSupplyId, out var supply) || !supply.IsActive || supply.Version != line.ExpectedSupplyVersion)
                    return Result<StockIssueSaved>.Failure(new Error("INV_STALE", "Vật tư đã thay đổi hoặc ngừng dùng. Tải lại và chọn lại vật tư."));
                if (!balances.TryGetValue(supply.Id, out var balance) || balance.OnHand < line.Quantity)
                    return Result<StockIssueSaved>.Failure(new Error("INV_INSUFFICIENT", $"Vật tư {supply.Code} không đủ tồn. Phiếu chưa được ghi; kiểm tra tồn và lượng thực tế."));
                if (balance.Version == long.MaxValue) return Result<StockIssueSaved>.Failure(new Error("INV_LIMIT", "Phiên bản tồn vượt giới hạn hỗ trợ."));
                balance.OnHand -= line.Quantity; balance.Version++; balance.UpdatedAt = now;
                issue.Lines.Add(new StockIssueLine { MedicalSupplyId = supply.Id, Quantity = line.Quantity, SupplyCode = supply.Code, SupplyName = supply.Name, Unit = supply.Unit });
            }
            db.StockIssues.Add(issue); await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog { UserId = actor, Action = "MOD_INV_ISSUE_CONFIRMED", EntityType = "StockIssue", EntityId = issue.Id, CreatedAt = now,
                Detail = JsonSerializer.Serialize(new { issue.VisitId, Lines = issue.Lines.Select(x => new { x.MedicalSupplyId, x.Quantity }) }) });
            await db.SaveChangesAsync(ct);
            var dto = await DtoAsync(issue, ct);
            await tx.CommitAsync(ct); committed = true;
            return Result<StockIssueSaved>.Success(new(dto, false));
        }
        catch (Exception e) when (Conflict(e)) { return Result<StockIssueSaved>.Failure(ConflictError); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static bool Conflict(Exception error)
    {
        for (Exception? e = error; e is not null; e = e.InnerException)
            if (e is DbUpdateConcurrencyException || e is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
