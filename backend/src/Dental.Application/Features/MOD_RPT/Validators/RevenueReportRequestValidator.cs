using Dental.Application.Features.MOD_RPT.DTOs;
using FluentValidation;

namespace Dental.Application.Features.MOD_RPT.Validators;

public sealed class RevenueReportRequestValidator : AbstractValidator<RevenueReportRequest>
{
    public RevenueReportRequestValidator()
    {
        RuleFor(x => x.FromDate).NotNull();
        RuleFor(x => x.ToDate).NotNull();
        RuleFor(x => x).Must(x => x.FromDate is { Year: >= 2 and <= 9998 } from &&
            x.ToDate is { Year: >= 2 and <= 9998 } to && to >= from && to.DayNumber - from.DayNumber <= 366)
            .WithMessage("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc; khoảng cách tối đa 366 ngày.");
        RuleFor(x => x.GroupBy).Must(x => x is "Auto" or "Day" or "Week" or "Month");
        RuleFor(x => x.Top).Must(x => x is "5" or "10" or "20" or "All");
        RuleFor(x => x.SortBy).Must(x => x is "Revenue" or "UsageCount");
        RuleFor(x => x.CashierId).GreaterThan(0).When(x => x.CashierId.HasValue);
    }
}
