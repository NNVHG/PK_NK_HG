using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Dental.Infrastructure.Data;

namespace Dental.Infrastructure.Repositories;

public sealed class BankTransferRepository(DentalDbContext db) : IBankTransferRepository
{
    public async Task<IReadOnlyDictionary<string, string>> GetBankSettingsAsync(CancellationToken ct = default)
        => await db.SystemConfigs.AsNoTracking().Where(x => x.Key == "Payment:BankBin" ||
            x.Key == "Payment:AccountNumber" || x.Key == "Payment:AccountName").ToDictionaryAsync(x => x.Key, x => x.Value, ct);
    public Task<Invoice?> GetInvoiceAsync(int invoiceId, CancellationToken ct = default)
        => db.Invoices.AsNoTracking().Include(x => x.Visit).FirstOrDefaultAsync(x => x.Id == invoiceId, ct);

    public async Task<Result<PaymentTransaction>> ConfirmManualAsync(int invoiceId, ManualBankTransferRequest request,
        int actorId, string role, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist))
            return Result<PaymentTransaction>.Failure(Error.Forbidden);
        if (invoiceId <= 0 || !(await new Dental.Application.Features.MOD_BIL.Validators.ManualBankTransferRequestValidator().ValidateAsync(request, ct)).IsValid)
            return Result<PaymentTransaction>.Failure(Error.Validation);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (!await db.Users.AnyAsync(u => u.UserId == actorId && u.IsActive && !u.RequiresPhoneActivation && u.Role.RoleCode == role, ct))
                return Result<PaymentTransaction>.Failure(Error.Forbidden);
            var existing = await db.PaymentTransactions.AsNoTracking().FirstOrDefaultAsync(p => p.RequestId == request.RequestId, ct);
            if (existing is not null)
            {
                if (existing.Source != "Manual" || existing.CashierId != actorId ||
                    !ManualBankTransferService.MatchesActualTransfer(existing, invoiceId, request.Amount, request.TransactionReference))
                    return Result<PaymentTransaction>.Failure(ManualBankTransferService.ReferenceConflict);
                await tx.CommitAsync(ct); committed = true;
                return Result<PaymentTransaction>.Success(existing);
            }
            existing = await db.PaymentTransactions.AsNoTracking().FirstOrDefaultAsync(p => p.TransactionReference == request.TransactionReference, ct);
            if (existing is not null)
            {
                if (!ManualBankTransferService.MatchesActualTransfer(existing, invoiceId, request.Amount, request.TransactionReference))
                    return Result<PaymentTransaction>.Failure(ManualBankTransferService.ReferenceConflict);
                await tx.CommitAsync(ct); committed = true;
                return Result<PaymentTransaction>.Success(existing);
            }
            var invoice = await db.Invoices.Include(i => i.Visit).FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
            if (invoice is null) return Result<PaymentTransaction>.Failure(Error.NotFound);
            if (invoice.FinalAmount - invoice.PaidAmount != request.ExpectedRemainingAmount)
                return Result<PaymentTransaction>.Failure(ManualBankTransferService.StaleBalance);
            var previousPaid = invoice.PaidAmount;
            var validation = BankTransferRules.Apply(invoice, request.Amount);
            if (validation.IsFailure) return Result<PaymentTransaction>.Failure(validation.Error);
            var credited = invoice.PaidAmount - previousPaid;
            var payment = new PaymentTransaction { InvoiceId = invoiceId, Amount = credited, BankReceivedAmount = request.Amount,
                AmountTendered = request.Amount, ChangeAmount = request.Amount - credited, PaymentMethod = "BankTransfer", Source = "Manual",
                TransactionReference = request.TransactionReference, RequestId = request.RequestId, CashierId = actorId,
                PaidAt = DateTime.UtcNow, Note = BankTransferRules.ExcessNote(request.Amount - credited) };
            db.PaymentTransactions.Add(payment); await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "MOD_BIL_BANK_MANUAL_CONFIRMED", EntityType = "Invoice",
                EntityId = invoiceId, CreatedAt = payment.PaidAt, IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { paymentId = payment.Id, source = "Manual", confirmed = true, requiresExcessReview = payment.ChangeAmount > 0 }) });
            if (payment.ChangeAmount > 0)
                db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "MOD_BIL_BANK_EXCESS_REVIEW", EntityType = "Invoice",
                    EntityId = invoiceId, CreatedAt = payment.PaidAt, IpAddress = ip,
                    Detail = JsonSerializer.Serialize(new { paymentId = payment.Id, source = "Manual", requiresExcessReview = true }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<PaymentTransaction>.Success(payment);
        }
        catch (Exception ex) when (IsWriteConflict(ex))
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result<PaymentTransaction>.Failure(new Error("BIL_006", "Có thao tác đồng thời; đối chiếu và thử lại cùng yêu cầu, không nhận thêm tiền."));
        }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }

    public async Task<Result<PaymentTransaction>> ReceiveAsync(BankTransferRequest request, string invoiceCode,
        int actorId, string? actorRole, bool simulated, bool manualConfirmation, string? ip, CancellationToken ct = default)
    {
        var source = simulated ? "Simulation" : manualConfirmation ? "Manual" : "Webhook";
        var requestId = BankTransferRules.RequestId(source, !simulated && !manualConfirmation && request.ProviderEventId.HasValue
            ? "SEPAY:" + request.ProviderEventId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : request.TransactionReference);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (manualConfirmation && (actorId <= 0 || actorRole is not (Dental.Domain.Constants.RoleCodes.Admin or Dental.Domain.Constants.RoleCodes.Receptionist) ||
                !await db.Users.AnyAsync(u => u.UserId == actorId && u.IsActive && u.Role.RoleCode == actorRole, ct)))
                return Result<PaymentTransaction>.Failure(Error.Forbidden);
            var existing = await db.PaymentTransactions.AsNoTracking().Include(x => x.Invoice)
                .FirstOrDefaultAsync(x => x.RequestId == requestId || x.TransactionReference == request.TransactionReference, ct);
            if (existing is not null)
            {
                // Q357a: a delayed real webhook can acknowledge the same manually reconciled transfer.
                if (!simulated && existing.Source == "Manual" && existing.Invoice.InvoiceCode == invoiceCode &&
                    ManualBankTransferService.MatchesActualTransfer(existing, existing.InvoiceId, request.Amount, request.TransactionReference))
                {
                    await tx.CommitAsync(ct); committed = true;
                    return Result<PaymentTransaction>.Success(existing);
                }
                // DL-195: an already stored bank reference is acknowledged without changing its original data.
                if (existing.Source != source || existing.TransactionReference != request.TransactionReference ||
                    (simulated || manualConfirmation) && (existing.Invoice.InvoiceCode != invoiceCode || existing.BankReceivedAmount != request.Amount || existing.CashierId != actorId))
                    return Result<PaymentTransaction>.Failure(Error.Conflict);
                await tx.CommitAsync(ct); committed = true;
                return Result<PaymentTransaction>.Success(existing);
            }
            var invoice = await db.Invoices.Include(x => x.Visit).FirstOrDefaultAsync(x => x.InvoiceCode == invoiceCode, ct);
            if (invoice is null) return Result<PaymentTransaction>.Failure(Error.NotFound);
            var previousPaid = invoice.PaidAmount;
            var result = BankTransferRules.Apply(invoice, request.Amount);
            if (result.IsFailure) return Result<PaymentTransaction>.Failure(result.Error);
            var credited = invoice.PaidAmount - previousPaid;
            var payment = new PaymentTransaction { InvoiceId = invoice.Id, Amount = credited, BankReceivedAmount = request.Amount,
                AmountTendered = request.Amount, ChangeAmount = request.Amount - credited, TransactionReference = request.TransactionReference,
                Source = source, Note = BankTransferRules.ExcessNote(request.Amount - credited), PaymentMethod = "BankTransfer",
                CashierId = actorId, PaidAt = DateTime.UtcNow, RequestId = requestId };
            db.PaymentTransactions.Add(payment); await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = simulated ? "MOD_BIL_BANK_SIMULATED" : manualConfirmation ? "MOD_BIL_BANK_MANUAL_CONFIRMED" : "MOD_BIL_BANK_RECEIVED",
                EntityType = "Invoice", EntityId = invoice.Id, CreatedAt = payment.PaidAt, IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { paymentId = payment.Id, source, requiresExcessReview = payment.ChangeAmount > 0 }) });
            if (payment.ChangeAmount > 0)
                db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "MOD_BIL_BANK_EXCESS_REVIEW", EntityType = "Invoice",
                    EntityId = invoice.Id, CreatedAt = payment.PaidAt, IpAddress = ip,
                    Detail = JsonSerializer.Serialize(new { paymentId = payment.Id, source, requiresExcessReview = true }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<PaymentTransaction>.Success(payment);
        }
        catch (Exception ex) when (IsWriteConflict(ex))
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result<PaymentTransaction>.Failure(new Error("BIL_006", "Có thao tác đồng thời; thử lại cùng mã giao dịch."));
        }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static bool IsWriteConflict(Exception error)
    {
        // Npgsql execution strategy can wrap a transient PG error in InvalidOperationException.
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
