using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dental.Infrastructure.Repositories;
public sealed class ReceiptRepository(DentalDbContext db) : IReceiptRepository
{
    private Task<PaymentTransaction?> LoadAsync(ReceiptRequest request, bool tracked, CancellationToken ct)
    {
        var query = db.PaymentTransactions.Include(p => p.Cashier).Include(p => p.Invoice).ThenInclude(i => i.Items)
            .Include(p => p.Invoice).ThenInclude(i => i.Refunds).ThenInclude(r => r.Items)
            .Include(p => p.Invoice).ThenInclude(i => i.Visit).ThenInclude(v => v.Patient).AsQueryable();
        if (!tracked) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(p => p.Id == request.PaymentId && p.InvoiceId == request.InvoiceId && p.Amount > 0, ct);
    }
    private async Task<ReceiptPrintDto> MapAsync(PaymentTransaction p, int number, DateTime? printedAt, CancellationToken ct)
    {
        var settings = await db.SystemConfigs.AsNoTracking().Where(s => s.Key == "Clinic:Address" || s.Key == "Clinic:Phone")
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        settings.TryGetValue("Clinic:Address", out var address); settings.TryGetValue("Clinic:Phone", out var phone);
        var invoice = p.Invoice; var patient = invoice.Visit.Patient;
        return new(invoice.Id, invoice.InvoiceCode, p.Id, "Nha khoa H&G", address, phone, patient.FullName,
            patient.PatientCode, patient.DateOfBirth.Year, p.Cashier.FullName, p.PaidAt, p.PaymentMethod, p.Source,
            p.Amount, p.AmountTendered, p.ChangeAmount, p.Note, invoice.FinalAmount, invoice.PaidAmount,
            invoice.FinalAmount - invoice.PaidAmount, p.PrintCount, number, printedAt,
            invoice.Items.OrderBy(i => i.Id).Select(i => new { Item = i, Quantity = i.Quantity - invoice.Refunds.SelectMany(r => r.Items).Where(r => r.InvoiceItemId == i.Id).Sum(r => r.Quantity) })
                .Where(x => x.Quantity > 0).Select(x => new ReceiptLineDto(x.Item.Name, x.Quantity, x.Item.UnitPrice, x.Quantity * x.Item.UnitPrice)).ToArray())
            { DiscountAmount = invoice.DiscountAmount, SubTotalAmount = invoice.TotalAmount };
    }
    public async Task<Result<ReceiptPrintDto>> GetAsync(ReceiptRequest request, CancellationToken ct = default)
    {
        var p = await LoadAsync(request, false, ct);
        if (p is null) return Result<ReceiptPrintDto>.Failure(Error.NotFound);
        if (p.PrintCount == int.MaxValue) return Result<ReceiptPrintDto>.Failure(Error.Conflict);
        return Result<ReceiptPrintDto>.Success(await MapAsync(p, p.PrintCount + 1, null, ct));
    }
    public async Task<Result<ReceiptPrintDto>> PrintAsync(ReceiptRequest request, int actorId, string? ip, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            var p = await LoadAsync(request, true, ct);
            if (p is null) return Result<ReceiptPrintDto>.Failure(Error.NotFound);
            if (p.PrintCount == int.MaxValue) return Result<ReceiptPrintDto>.Failure(Error.Conflict);
            p.PrintCount++; var now = DateTime.UtcNow;
            db.AuditLogs.Add(new AuditLog { Action = p.PrintCount == 1 ? "PrintReceipt" : "ReprintReceipt",
                EntityType = "PaymentTransaction", EntityId = p.Id, UserId = actorId, IpAddress = ip, CreatedAt = now,
                Detail = JsonSerializer.Serialize(new { paymentId = p.Id, invoiceId = p.InvoiceId, printNumber = p.PrintCount }) });
            var receipt = await MapAsync(p, p.PrintCount, now, ct);
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); committed = true;
            return Result<ReceiptPrintDto>.Success(receipt);
        }
        catch (Exception error) when (IsConflict(error))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<ReceiptPrintDto>.Failure(new Error("BIL_061", "Có yêu cầu in đồng thời; tải lại biên lai để đối chiếu lượt in."));
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
