using Dental.Application.Features.Staff.DTOs;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.Staff.Validators;

public sealed class StaffQueryRequestValidator : AbstractValidator<StaffQueryRequest>
{
    public StaffQueryRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Số trang phải lớn hơn 0.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Số dòng mỗi trang phải từ 1 đến 100.");
        RuleFor(x => x.RoleCode)
            .Must(IsStaffRole)
            .WithMessage("Vai trò lọc không hợp lệ.")
            .When(x => !string.IsNullOrWhiteSpace(x.RoleCode));
        RuleFor(x => x.Search)
            .MaximumLength(100)
            .WithMessage("Từ khóa tìm kiếm không được vượt quá 100 ký tự.");
    }

    private static bool IsStaffRole(string? roleCode)
        => RoleCodes.StaffRoles.Contains(roleCode?.Trim().ToUpperInvariant() ?? string.Empty);
}
