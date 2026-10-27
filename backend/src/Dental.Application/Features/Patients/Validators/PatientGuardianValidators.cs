using FluentValidation;

namespace Dental.Application.Features.Patients.Validators;

public sealed class AssignPatientGuardianRequestValidator : AbstractValidator<DTOs.AssignPatientGuardianRequest>
{
    public AssignPatientGuardianRequestValidator()
    {
        RuleFor(x => x.GuardianPatientId).GreaterThan(0).WithMessage("GuardianPatientId phải lớn hơn 0.");
        RuleFor(x => x.WardPatientId).GreaterThan(0).WithMessage("WardPatientId phải lớn hơn 0.");
        RuleFor(x => x).Must(x => x.GuardianPatientId != x.WardPatientId)
            .WithMessage("Hồ sơ giám hộ và người được giám hộ không thể là cùng một người.");
        RuleFor(x => x.Relationship)
            .NotEmpty().WithMessage("Mối quan hệ không được để trống.")
            .MaximumLength(50).WithMessage("Mối quan hệ không vượt quá 50 ký tự.");
        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Ghi chú không vượt quá 500 ký tự.");
    }
}

public sealed class RevokePatientGuardianRequestValidator : AbstractValidator<DTOs.RevokePatientGuardianRequest>
{
    public RevokePatientGuardianRequestValidator()
    {
        RuleFor(x => x.PatientGuardianId).GreaterThan(0).WithMessage("Quan hệ giám hộ không hợp lệ.");
        RuleFor(x => x.ExpectedAssignedAt).NotEqual(default(DateTime)).WithMessage("Thiếu phiên bản quan hệ giám hộ.");
        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Ghi chú không vượt quá 500 ký tự.");
    }
}

public sealed class PromoteWardRequestValidator : AbstractValidator<DTOs.PromoteWardRequest>
{
    public PromoteWardRequestValidator()
    {
        RuleFor(x => x.RawCitizenId)
            .NotEmpty().WithMessage("CCCD không được để trống.")
            .Matches(@"^\d{12}$").WithMessage("CCCD phải gồm đúng 12 chữ số.");
        RuleFor(x => x.Phone)
            .Matches(@"^(0\d{9,10}|\+84\d{9,10})$").When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Số điện thoại không hợp lệ.");
        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Ghi chú không vượt quá 500 ký tự.");
    }
}
