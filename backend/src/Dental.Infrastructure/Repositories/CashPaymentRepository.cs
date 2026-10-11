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

public sealed class CashPaymentRepository(DentalDbContext db) : ICashPaymentRepository
{
    public Task<bool> InvoiceExistsAsync(int invoiceId, CancellationToken ct = default)
        => db.Invoices.AsNoTracking().AnyAsync(x => x.Id == invoiceId, ct);

    public async Task<IReadOnlyList<PaymentTransaction>> GetAsync(int invoiceId, CancellationToken ct = default)
        => await db.PaymentTransactions.AsNoTracking().Where(x => x.InvoiceId == invoiceId).OrderBy(x => x.Id).ToListAsync(ct);

    public async Task<Result<PaymentTransaction>> ReceiveAsync(int invoiceId, int cashierId, CashPaymentRequest request, string? ip, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            var existing = await db.PaymentTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.RequestId == request.RequestId, ct);
            if (existing is not null)
            {
                if (!CashPaymentRules.IsSameRequest(existing, invoiceId, cashierId, request))
                    return Result<PaymentTransaction>.Failure(Error.Conflict);
                await transaction.CommitAsync(ct); committed = true;
                return Result<PaymentTransaction>.Success(existing);
            }
            var invoice = await db.Invoices.Include(x => x.Visit).FirstOrDefaultAsync(x => x.Id == invoiceId, ct);
            if (invoice is null) return Result<PaymentTransaction>.Failure(Error.NotFound);
            var validation = CashPaymentRules.Apply(invoice, request.Amount);
            if (validation.IsFailure) return Result<PaymentTransaction>.Failure(validation.Error);
            var payment = new PaymentTransaction { InvoiceId = invoiceId, Amount = request.Amount,
                AmountTendered = request.AmountTendered, ChangeAmount = request.AmountTendered - request.Amount,
                CashierId = cashierId, PaidAt = DateTime.UtcNow, RequestId = request.RequestId, PaymentMethod = "Cash" };
            db.PaymentTransactions.Add(payment);
            await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog { Action = "MOD_BIL_CASH_RECEIVED", EntityType = "Invoice", EntityId = invoiceId,
                UserId = cashierId, CreatedAt = payment.PaidAt, IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { paymentId = payment.Id, action = "cash_received" }) });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct); committed = true;
            return Result<PaymentTransaction>.Success(payment);
        }
        catch (Exception error) when (error is PostgresException { SqlState: "40001" or "40P01" or "23505" } ||
            error is DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" or "23505" } })
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PaymentTransaction>.Failure(new Error("BIL_006", "Có thao tác đồng thời; tải lại hoặc thử lại cùng yêu cầu."));
        }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
}
