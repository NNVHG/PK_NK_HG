namespace Dental.Application.Features.MOD_BIL.DTOs;

public sealed record ManualBankTransferRequest(Guid RequestId, decimal Amount, string TransactionReference,
    decimal ExpectedRemainingAmount, bool Confirmed);
