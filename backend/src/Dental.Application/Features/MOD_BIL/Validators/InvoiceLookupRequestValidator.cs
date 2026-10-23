using Dental.Application.Features.MOD_BIL.DTOs;
using FluentValidation;
namespace Dental.Application.Features.MOD_BIL.Validators;
public sealed class InvoiceLookupRequestValidator : AbstractValidator<InvoiceLookupRequest>
{
    public InvoiceLookupRequestValidator()
        => RuleFor(x => x.Code).Must(IsValidCode).WithMessage("Mã hóa đơn gồm 1–50 chữ/số ASCII hoặc dấu gạch nối.");
    public static bool IsValidCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var value = code.Trim();
        return value.Length <= 50 && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
    }
}
