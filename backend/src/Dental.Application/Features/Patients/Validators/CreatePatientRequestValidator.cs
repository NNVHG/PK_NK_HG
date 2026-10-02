using Dental.Application.Features.Patients.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Patients.Validators;

public sealed class CreatePatientRequestValidator : AbstractValidator<CreatePatientRequest>
{
    public CreatePatientRequestValidator()
    {
        RuleFor(request => request.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Họ tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ tên không được vượt quá 100 ký tự.");

        RuleFor(request => request.DateOfBirth)
            .Must(dateOfBirth => dateOfBirth <= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Ngày sinh không được ở tương lai.")
            .Must(dateOfBirth => dateOfBirth >= DateOnly.FromDateTime(DateTime.Today).AddYears(-120))
            .WithMessage("Ngày sinh không được quá 120 tuổi.");

        RuleFor(request => request.Gender)
            .Must(IsValidGender)
            .WithMessage("Giới tính không hợp lệ.");

        RuleFor(request => request.Phone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại không hợp lệ (định dạng: 0xxxxxxxxx).");

        RuleFor(request => request.Email)
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(100).WithMessage("Email không được vượt quá 100 ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Email));

        RuleFor(request => request.Address)
            .MaximumLength(500).WithMessage("Địa chỉ không được vượt quá 500 ký tự.");
    }

    private static bool IsValidGender(string? gender)
        => string.IsNullOrWhiteSpace(gender) || gender is "Male" or "Female" or "Other";
}
