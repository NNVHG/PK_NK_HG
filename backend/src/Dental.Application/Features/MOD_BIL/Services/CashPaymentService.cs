using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.MOD_BIL.Services;

public sealed class CashPaymentService(ICashPaymentRepository repository, CashPaymentRequestValidator validator)
{
    public async Task<Result<CashPaymentResponse>> ReceiveAsync(int invoiceId, int cashierId, string? role,
        CashPaymentRequest request, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist)) return Result<CashPaymentResponse>.Failure(Error.Forbidden);
        if (invoiceId <= 0 || !(await validator.ValidateAsync(request, ct)).IsValid) return Result<CashPaymentResponse>.Failure(Error.Validation);
        var result = await repository.ReceiveAsync(invoiceId, cashierId, request, ip, ct);
        return result.IsFailure ? Result<CashPaymentResponse>.Failure(result.Error) : Result<CashPaymentResponse>.Success(Map(result.Value!));
    }

    public static CashPaymentResponse Map(PaymentTransaction p)
        => new(p.Id, p.InvoiceId, p.Amount, p.AmountTendered, p.ChangeAmount, p.PaymentMethod, p.CashierId, p.PaidAt, p.RequestId);
}
