using Dental.Application.Features.Auth.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Auth.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại không hợp lệ (định dạng: 0xxxxxxxxx).");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự.");
    }
}
