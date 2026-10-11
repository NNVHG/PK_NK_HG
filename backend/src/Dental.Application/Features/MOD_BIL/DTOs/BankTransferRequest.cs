namespace Dental.Application.Features.MOD_BIL.DTOs;

// Normalized inbound contract; never trust an invoice ID, actor or credited amount from the sender.
public sealed record BankTransferRequest(string AddInfo, decimal Amount, string TransactionReference, long? ProviderEventId = null);
public sealed record SePayWebhookRequest(long Id, string TransferType, decimal TransferAmount, string ReferenceCode,
    string Content, string AccountNumber);
public sealed record BankTransferSimulationRequest(string InvoiceCode, decimal Amount, string TransactionReference);
public sealed record BankTransferResponse(int Id, int InvoiceId, decimal Amount, decimal BankReceivedAmount,
    decimal ChangeAmount, string TransactionReference, string Source, string? Note, DateTime PaidAt);
public sealed record BankTransferInfo(int InvoiceId, string InvoiceCode, decimal RemainingAmount,
    string Content, string? QrUrl, string? AccountName, bool SimulationEnabled, string? ConfigurationMessage);
