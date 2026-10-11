using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;

namespace Dental.Application.Features.MOD_BIL.Services;

public sealed class BankTransferWebhookService(IBankTransferRepository repository, BankTransferRequestValidator validator)
{
    public async Task<Result<BankTransferResponse>> ReceiveAsync(BankTransferRequest request, int actorId,
        bool simulated, string? ip, CancellationToken ct = default)
    {
        if (actorId <= 0) return Result<BankTransferResponse>.Failure(Error.Forbidden);
        if (!(await validator.ValidateAsync(request, ct)).IsValid) return Result<BankTransferResponse>.Failure(Error.Validation);
        var result = await repository.ReceiveAsync(request, BankTransferRules.InvoiceCode(request.AddInfo)!, actorId, simulated, ip, ct);
        if (result.IsFailure) return Result<BankTransferResponse>.Failure(result.Error);
        var p = result.Value!;
        return Result<BankTransferResponse>.Success(new(p.Id, p.InvoiceId, p.Amount, p.BankReceivedAmount!.Value,
            p.ChangeAmount!.Value, p.TransactionReference!, p.Source!, p.Note, p.PaidAt));
    }
}
