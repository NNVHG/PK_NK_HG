using Dental.Application.Features.Auth.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Auth.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Nhập SĐT hoặc CCCD.")
            .Must(x => x is not null && System.Text.RegularExpressions.Regex.IsMatch(x, @"^(0\d{9}|\d{12})$"))
            .WithMessage("Nhập SĐT gồm 10 số hoặc CCCD gồm 12 số.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự.");
    }
}
