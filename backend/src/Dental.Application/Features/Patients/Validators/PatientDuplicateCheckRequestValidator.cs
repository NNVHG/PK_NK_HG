using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Patients.Validators;

public sealed class PatientDuplicateCheckRequestValidator : AbstractValidator<PatientDuplicateCheckRequest>
{
    public PatientDuplicateCheckRequestValidator(VietnamClock vietnamClock)
    {
        RuleFor(request => request.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Họ tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ tên không được vượt quá 100 ký tự.");

        RuleFor(request => request.DateOfBirth)
            .Must(dateOfBirth => dateOfBirth <= vietnamClock.Today)
            .WithMessage("Ngày sinh không được ở tương lai.")
            .Must(dateOfBirth => dateOfBirth >= vietnamClock.Today.AddYears(-120))
            .WithMessage("Ngày sinh không được quá 120 tuổi.");

        RuleFor(request => request.Phone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại không hợp lệ (định dạng: 0xxxxxxxxx).");
    }
}
