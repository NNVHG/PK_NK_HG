using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using FluentValidation;

namespace Dental.Application.Features.MOD_BIL.Validators;

public sealed class DiscountPresetRequestValidator : AbstractValidator<DiscountPresetRequest>
{
    public DiscountPresetRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9_-]+$");
        RuleFor(x => x.Name).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(100);
        RuleFor(x => x).Must(x => InvoiceDiscountRules.ValidValue(x.DiscountType, x.Value));
    }
}

public sealed class ApplyInvoiceDiscountRequestValidator : AbstractValidator<ApplyInvoiceDiscountRequest>
{
    public ApplyInvoiceDiscountRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(255);
        RuleFor(x => x.ExpectedTotalAmount).InclusiveBetween(0m, InvoiceServiceSnapshot.MaximumAmount).Must(IsWhole);
        RuleFor(x => x.ExpectedCurrentDiscountAmount).InclusiveBetween(0m, InvoiceServiceSnapshot.MaximumAmount).Must(IsWhole);
        RuleFor(x => x.ExpectedDiscountAmount).InclusiveBetween(0m, InvoiceServiceSnapshot.MaximumAmount).Must(IsWhole);
        RuleFor(x => x).Must(x => x.Mode switch
        {
            "Preset" => x.PresetId > 0 && x.DiscountType is null && x.Value is null,
            "Custom" => x.PresetId is null && x.Value.HasValue && InvoiceDiscountRules.ValidValue(x.DiscountType, x.Value.Value),
            "Remove" => x.PresetId is null && x.DiscountType is null && x.Value is null && x.ExpectedDiscountAmount == 0,
            _ => false
        });
    }
    private static bool IsWhole(decimal value) => decimal.Truncate(value) == value;
}
