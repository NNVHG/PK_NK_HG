using Dental.Application.Features.MOD_MST.DTOs;
using FluentValidation;
namespace Dental.Application.Features.MOD_MST.Validators;
public sealed class MedicineQueryValidator : AbstractValidator<MedicineQuery>
{
    public MedicineQueryValidator()
    {
        RuleFor(x => x.Keyword).MaximumLength(200);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
public sealed class MedicineWriteValidator : AbstractValidator<MedicineWriteRequest>
{
    public static readonly IReadOnlyList<string> Units = Array.AsReadOnly(new[] { "Viên", "Vỉ", "Gói", "Chai", "Ống", "Tuýp", "Hộp" });
    public MedicineWriteValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9._-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ActiveIngredient).MaximumLength(200); RuleFor(x => x.Strength).MaximumLength(50);
        RuleFor(x => x.Unit).Must(x => Units.Contains(x));
        RuleFor(x => x.UnitPrice).InclusiveBetween(0, 999999999999999999m).Must(x => x == decimal.Truncate(x));
        RuleFor(x => x.UnitPrice).GreaterThan(0).When(x => x.IsActive).WithMessage("Đặt giá lớn hơn 0 trước khi kích hoạt thuốc.");
        RuleFor(x => x.DefaultUsageInstruction).MaximumLength(500);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}
