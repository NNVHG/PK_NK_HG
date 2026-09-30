using Dental.Application.Features.Staff.DTOs;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.Staff.Validators;

public sealed class CreateStaffRequestValidator : AbstractValidator<CreateStaffRequest>
{
    public CreateStaffRequestValidator()
    {
        RuleFor(x => x.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Họ tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ tên không được vượt quá 100 ký tự.");
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^0\d{9}$").WithMessage("Số điện thoại không hợp lệ (định dạng: 0xxxxxxxxx).");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu ban đầu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự.")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ số.");
        RuleFor(x => x.RoleCode)
            .Must(IsStaffRole)
            .WithMessage("Vai trò nhân viên không hợp lệ.");
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(100).WithMessage("Email không được vượt quá 100 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Gender)
            .Must(IsGender)
            .WithMessage("Giới tính không hợp lệ.");
    }

    private static bool IsStaffRole(string? roleCode)
        => RoleCodes.StaffRoles.Contains(roleCode?.Trim().ToUpperInvariant() ?? string.Empty);

    private static bool IsGender(string? gender)
        => string.IsNullOrWhiteSpace(gender) || gender is "Male" or "Female" or "Other";
}
