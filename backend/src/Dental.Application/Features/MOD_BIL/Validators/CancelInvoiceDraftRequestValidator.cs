using Dental.Application.Features.MOD_BIL.DTOs;
using FluentValidation;

namespace Dental.Application.Features.MOD_BIL.Validators;

public sealed class CancelInvoiceDraftRequestValidator : AbstractValidator<CancelInvoiceDraftRequest>
{
    public CancelInvoiceDraftRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("Lý do hủy hóa đơn bắt buộc, tối đa 500 ký tự.");
    }
}
