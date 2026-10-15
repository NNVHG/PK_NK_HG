using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;

public sealed class InvoiceDiscountRepository(DentalDbContext db) : IInvoiceDiscountRepository
{
    public async Task<IReadOnlyList<DiscountPreset>> GetPresetsAsync(bool includeInactive, CancellationToken ct = default)
        => await db.DiscountPresets.AsNoTracking().Where(x => includeInactive || x.IsActive).OrderBy(x => x.Code).ToArrayAsync(ct);

    public async Task<Result<DiscountPreset>> SavePresetAsync(int? id, DiscountPresetRequest request, int actorId, string? ip, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            var preset = id.HasValue ? await db.DiscountPresets.FindAsync([id.Value], ct) : new DiscountPreset();
            if (preset is null) return Result<DiscountPreset>.Failure(Error.NotFound);
            if (await db.DiscountPresets.AnyAsync(x => x.Code == request.Code && x.Id != (id ?? 0), ct))
                return Result<DiscountPreset>.Failure(new Error("BIL_026", "Mã mẫu giảm giá đã tồn tại."));
            preset.Code = request.Code; preset.Name = request.Name; preset.DiscountType = request.DiscountType;
            preset.Value = request.Value; preset.IsActive = request.IsActive;
            if (!id.HasValue) db.DiscountPresets.Add(preset);
            await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog { Action = "MOD_BIL_DISCOUNT_PRESET_SAVED", EntityType = "DiscountPreset",
                EntityId = preset.Id, UserId = actorId, IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { preset.DiscountType, preset.Value, preset.IsActive }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<DiscountPreset>.Success(preset);
        }
        catch (Exception error) when (IsConflict(error))
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result<DiscountPreset>.Failure(new Error("BIL_006", "Mẫu giảm giá đã thay đổi hoặc trùng mã; tải lại trước khi thử lại."));
        }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }

    public async Task<Result<InvoiceDiscountResponse>> ApplyAsync(int invoiceId, ApplyInvoiceDiscountRequest request,
        int actorId, string role, string? ip, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            var invoice = await db.Invoices.Include(x => x.Items).Include(x => x.Visit).FirstOrDefaultAsync(x => x.Id == invoiceId, ct);
            if (invoice is null) return Result<InvoiceDiscountResponse>.Failure(Error.NotFound);
            if (await db.PaymentTransactions.AnyAsync(x => x.InvoiceId == invoiceId, ct))
                return Result<InvoiceDiscountResponse>.Failure(new Error("BIL_021", "Hóa đơn đã có khoản thu; không thể sửa giảm giá."));
            var preset = request.PresetId.HasValue ? await db.DiscountPresets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.PresetId, ct) : null;
            var oldAmount = invoice.DiscountAmount;
            var applied = InvoiceDiscountRules.Apply(invoice, preset, request, role);
            if (applied.IsFailure) return Result<InvoiceDiscountResponse>.Failure(applied.Error);
            db.AuditLogs.Add(new AuditLog { Action = request.Mode == "Remove" ? "MOD_BIL_DISCOUNT_REMOVED" : "MOD_BIL_DISCOUNT_APPLIED",
                EntityType = "Invoice", EntityId = invoice.Id, UserId = actorId, IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { oldAmount, invoice.DiscountAmount, invoice.DiscountType,
                    invoice.DiscountValue, invoice.DiscountPresetId, invoice.Status, reason = invoice.DiscountReason, role }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<InvoiceDiscountResponse>.Success(new(invoice.Id, invoice.TotalAmount, invoice.DiscountAmount,
                invoice.FinalAmount, invoice.FinalAmount - invoice.PaidAmount));
        }
        catch (Exception error) when (IsConflict(error))
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result<InvoiceDiscountResponse>.Failure(new Error("BIL_006", "Có thao tác tài chính đồng thời; tải lại hóa đơn và xác nhận lại."));
        }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }

    private static bool IsConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException || current is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
