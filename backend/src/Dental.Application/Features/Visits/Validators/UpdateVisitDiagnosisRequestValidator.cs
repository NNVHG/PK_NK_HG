using Dental.Application.Features.Visits.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Visits.Validators;

public sealed class UpdateVisitDiagnosisRequestValidator : AbstractValidator<UpdateVisitDiagnosisRequest>
{
    public UpdateVisitDiagnosisRequestValidator()
    {
        RuleFor(x => x.Diagnosis)
            .NotEmpty()
            .WithMessage("Chẩn đoán không được để trống.")
            .MaximumLength(1000)
            .WithMessage("Chẩn đoán không được vượt quá 1000 ký tự.");

        RuleFor(x => x.ClinicalNotes)
            .MaximumLength(2000)
            .WithMessage("Ghi chú lâm sàng không được vượt quá 2000 ký tự.");
    }
}
