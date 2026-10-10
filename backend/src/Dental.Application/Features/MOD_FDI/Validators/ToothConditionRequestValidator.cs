using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.MOD_FDI.Validators;

public sealed class ToothConditionRequestValidator : AbstractValidator<ToothConditionRequest>
{
    public ToothConditionRequestValidator()
    {
        RuleFor(x => x.ToothNumber).Must(FdiCodes.IsTooth).WithMessage("Số răng không thuộc chuẩn FDI.");
        RuleFor(x => x.Surface).Must(FdiCodes.IsSurface).WithMessage("Mặt răng phải là B, L, M, D, O, tổ hợp không trùng, All hoặc null.");
        RuleFor(x => x.ConditionCode).Must(x => FdiCodes.Conditions.Contains(x)).WithMessage("Mã tình trạng răng không hợp lệ.");
    }
}
