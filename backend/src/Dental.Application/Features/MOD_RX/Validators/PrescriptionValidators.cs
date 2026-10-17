using Dental.Application.Features.MOD_RX.DTOs;
using FluentValidation;
namespace Dental.Application.Features.MOD_RX.Validators;
public sealed class PrescriptionWriteValidator : AbstractValidator<PrescriptionWriteRequest>
{
    public PrescriptionWriteValidator()
    {
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Diagnosis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.DoctorAdvice).MaximumLength(1000);
        RuleFor(x => x.Items).NotNull().Must(x => x is { Count: >= 1 and <= 10 }).WithMessage("Đơn phải có từ 1 đến 10 loại thuốc.");
        RuleFor(x => x.Items).Must(x => x is not null && x.All(i => i is not null) && x.Select(i => i.MedicineId).Distinct().Count() == x.Count).WithMessage("Không được trùng thuốc.");
        RuleForEach(x => x.Items).NotNull().SetValidator(new PrescriptionLineValidator());
        RuleFor(x => x.PreviewToken).Matches("^[A-Fa-f0-9]{64}$").When(x => x.PreviewToken is not null);
    }
}
public sealed class PrescriptionLineValidator : AbstractValidator<PrescriptionLineRequest>
{
    public PrescriptionLineValidator()
    {
        RuleFor(x => x.MedicineId).GreaterThan(0); RuleFor(x => x.MedicineVersion).GreaterThan(0);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100);
        RuleFor(x => x.UsageInstructions).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TreatmentDays).InclusiveBetween(1, 30).When(x => x.TreatmentDays.HasValue);
        RuleFor(x => x.AllergyOverrideReason).MaximumLength(500);
    }
}
