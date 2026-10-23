using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;

namespace Dental.Application.Features.MOD_BIL.Services;

public sealed class BankTransferWebhookService(IBankTransferRepository repository, BankTransferRequestValidator validator)
{
    public async Task<Result<BankTransferResponse>> ReceiveAsync(BankTransferRequest request, int actorId,
        string? actorRole, bool simulated, bool manualConfirmation, string? ip, CancellationToken ct = default)
    {
        if (actorId <= 0) return Result<BankTransferResponse>.Failure(Error.Forbidden);
        if (!(await validator.ValidateAsync(request, ct)).IsValid) return Result<BankTransferResponse>.Failure(Error.Validation);
        var result = await repository.ReceiveAsync(request, BankTransferRules.InvoiceCode(request.AddInfo)!, actorId,
            actorRole, simulated, manualConfirmation, ip, ct);
        if (result.IsFailure) return Result<BankTransferResponse>.Failure(result.Error);
        var p = result.Value!;
        return Result<BankTransferResponse>.Success(new(p.Id, p.InvoiceId, p.Amount, p.BankReceivedAmount!.Value,
            p.ChangeAmount!.Value, p.TransactionReference!, p.Source!, p.Note, p.PaidAt));
    }

    public async Task<Result<BankTransferResponse>> ConfirmManualAsync(int invoiceId, int actorId, string? actorRole,
        ManualBankTransferConfirmationRequest request, string? ip, CancellationToken ct = default)
    {
        if (actorRole is not (Dental.Domain.Constants.RoleCodes.Admin or Dental.Domain.Constants.RoleCodes.Receptionist))
            return Result<BankTransferResponse>.Failure(Error.Forbidden);
        if (invoiceId <= 0 || !request.Confirmed)
            return Result<BankTransferResponse>.Failure(Error.Validation);
        var invoice = await repository.GetInvoiceAsync(invoiceId, ct);
        if (invoice is null) return Result<BankTransferResponse>.Failure(Error.NotFound);
        var transfer = new BankTransferRequest($"PKNK {invoice.InvoiceCode}", request.Amount, request.TransactionReference);
        if (!(await validator.ValidateAsync(transfer, ct)).IsValid)
            return Result<BankTransferResponse>.Failure(Error.Validation);
        var result = await repository.ReceiveAsync(transfer, invoice.InvoiceCode, actorId, actorRole,
            simulated: false, manualConfirmation: true, ip: ip, ct: ct);
        if (result.IsFailure) return Result<BankTransferResponse>.Failure(result.Error);
        var p = result.Value!;
        return Result<BankTransferResponse>.Success(new(p.Id, p.InvoiceId, p.Amount, p.BankReceivedAmount!.Value,
            p.ChangeAmount!.Value, p.TransactionReference!, p.Source!, p.Note, p.PaidAt));
    }
}
