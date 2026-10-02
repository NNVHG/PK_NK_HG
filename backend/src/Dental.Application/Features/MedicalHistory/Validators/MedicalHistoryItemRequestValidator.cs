using Dental.Application.Features.MedicalHistory.DTOs;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.MedicalHistory.Validators;

public sealed class MedicalHistoryItemRequestValidator : AbstractValidator<MedicalHistoryItemRequest>
{
    public MedicalHistoryItemRequestValidator()
    {
        RuleFor(item => item.Type)
            .Must(type => MedicalHistoryTypes.All.Contains(type))
            .WithMessage("Loại tiền sử phải là Allergy hoặc Condition.");

        RuleFor(item => item.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Tên bệnh hoặc dị ứng không được để trống.")
            .MaximumLength(200).WithMessage("Tên bệnh hoặc dị ứng không được vượt quá 200 ký tự.");

        RuleFor(item => item.Detail)
            .MaximumLength(1000).WithMessage("Chi tiết không được vượt quá 1000 ký tự.");
    }
}
