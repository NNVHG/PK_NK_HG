using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Auth.Validators;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator(VietnamClock vietnamClock)
    {
        RuleFor(x => x.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Họ tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ tên không được vượt quá 100 ký tự.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại không hợp lệ (định dạng: 0xxxxxxxxx).");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(100).WithMessage("Email không được vượt quá 100 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        // [CẦN XÁC NHẬN] Tài liệu chưa xác định ngày sinh và giới tính có bắt buộc hay không; hiện cho phép để trống.

        RuleFor(x => x.DateOfBirth)
            .Must(dateOfBirth => !dateOfBirth.HasValue || dateOfBirth.Value <= vietnamClock.Today)
            .WithMessage("Ngày sinh không được ở tương lai.")
            // [CẦN XÁC NHẬN] Giới hạn tuổi này dùng cùng quy tắc validator hồ sơ bệnh nhân hiện có.
            .Must(dateOfBirth => !dateOfBirth.HasValue || dateOfBirth.Value >= vietnamClock.Today.AddYears(-120))
            .WithMessage("Ngày sinh không được quá 120 tuổi.");

        RuleFor(x => x.Gender)
            .Must(gender => string.IsNullOrWhiteSpace(gender) ||
                gender is "Male" or "Female" or "Other")
            .WithMessage("Giới tính không hợp lệ.");
    }
}
