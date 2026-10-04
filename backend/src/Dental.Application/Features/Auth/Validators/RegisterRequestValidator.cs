using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Auth.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator(VietnamClock vietnamClock)
    {
        RuleFor(request => request.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ tên không được vượt quá 100 ký tự.");

        RuleFor(request => request.Phone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại không hợp lệ (định dạng: 0xxxxxxxxx).");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu mới phải có ít nhất 6 ký tự.")
            .Matches(@"[A-Z]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ hoa.")
            .Matches(@"[a-z]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ thường.")
            .Matches(@"[0-9]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ số.");

        RuleFor(request => request.DateOfBirth)
            .NotEmpty().WithMessage("Ngày sinh không được để trống.")
            .Must(dateOfBirth => dateOfBirth <= vietnamClock.Today)
            .WithMessage("Ngày sinh không được ở tương lai.")
            // [CẦN XÁC NHẬN] Giới hạn tuổi này dùng cùng quy tắc validator hồ sơ bệnh nhân hiện có.
            .Must(dateOfBirth => dateOfBirth >= vietnamClock.Today.AddYears(-120))
            .WithMessage("Ngày sinh không được quá 120 tuổi.");

        RuleFor(request => request.Gender)
            .Must(gender => gender is "Male" or "Female" or "Other")
            .WithMessage("Giới tính không hợp lệ.");

        RuleFor(request => request.Email)
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(100).WithMessage("Email không được vượt quá 100 ký tự.")
            .When(request => !string.IsNullOrWhiteSpace(request.Email));
    }
}
