using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.MOD_BIL.Services;

public sealed class ManualBankTransferService(IBankTransferRepository repository, ManualBankTransferRequestValidator validator)
{
    public static readonly Error StaleBalance = new("BIL_064", "Số dư hóa đơn đã thay đổi. Tải lại, đối chiếu và xác nhận lại; chưa ghi khoản thu này.");
    public static readonly Error ReferenceConflict = new("BIL_065", "Mã yêu cầu hoặc tham chiếu đã được dùng cho giao dịch khác. Đối chiếu lịch sử trước khi tiếp tục.");

    public async Task<Result<BankTransferResponse>> ConfirmAsync(int invoiceId, ManualBankTransferRequest request,
        int actorId, string? role, string? ip, CancellationToken ct = default)
    {
        if (actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Receptionist))
            return Result<BankTransferResponse>.Failure(Error.Forbidden);
        if (invoiceId <= 0 || !(await validator.ValidateAsync(request, ct)).IsValid)
            return Result<BankTransferResponse>.Failure(Error.Validation);
        var result = await repository.ConfirmManualAsync(invoiceId, request, actorId, role, ip, ct);
        if (result.IsFailure) return Result<BankTransferResponse>.Failure(result.Error);
        var p = result.Value!;
        return Result<BankTransferResponse>.Success(new(p.Id, p.InvoiceId, p.Amount, p.BankReceivedAmount!.Value,
            p.ChangeAmount!.Value, p.TransactionReference!, p.Source!, p.Note, p.PaidAt));
    }

    // Cross-source replay is valid only for the same actual transfer, never a simulated receipt.
    public static bool MatchesActualTransfer(PaymentTransaction payment, int invoiceId, decimal amount, string reference)
        => payment.InvoiceId == invoiceId && payment.PaymentMethod == "BankTransfer" && payment.TransactionType == "Receipt"
            && payment.Source is "Manual" or "Webhook" && payment.BankReceivedAmount == amount
            && payment.TransactionReference == reference;
}
