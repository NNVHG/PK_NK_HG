using FluentValidation;

namespace Dental.Application.Features.MOD_BIL.Validators;

public sealed record UnlockVisitRequest(string UnlockReason);
public sealed class UnlockVisitRequestValidator : AbstractValidator<UnlockVisitRequest>
{
    public UnlockVisitRequestValidator() => RuleFor(x => x.UnlockReason).NotEmpty().MaximumLength(500)
        .Must(reason => reason is not null && reason.Trim().Length >= 10)
        .WithMessage("Lý do mở khóa phải có từ 10 đến 500 ký tự.");
}
