using Dental.Application.Features.Auth.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Auth.Validators;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.OldPassword)
            .NotEmpty().WithMessage("Mật khẩu cũ không được để trống.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu mới không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự.")
            .MaximumLength(72).WithMessage("Mật khẩu mới không được vượt 72 byte UTF-8.")
            .Must(x => x is not null && !x.Contains('\0') && System.Text.Encoding.UTF8.GetByteCount(x) <= 72)
            .WithMessage("Mật khẩu mới không được chứa ký tự NUL hoặc vượt 72 byte UTF-8.")
            .Matches(@"[A-Z]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ hoa.")
            .Matches(@"[a-z]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ thường.")
            .Matches(@"[0-9]").WithMessage("Mật khẩu mới phải chứa ít nhất 1 chữ số.");
    }
}
