using Dental.Application.Features.MOD_BIL.DTOs;
using FluentValidation;
namespace Dental.Application.Features.MOD_BIL.Validators;
public sealed class InvoicePortalListQueryValidator : AbstractValidator<InvoicePortalListQuery>
{
    public InvoicePortalListQueryValidator()
    {
        RuleFor(x => x.Status).Must(status => status is null or >= 0 and <= 4);
        // Keep the UTC+7 and exclusive next-day conversions inside DateTime's range.
        RuleFor(x => x.FromDate).Must(date => date is null || date >= new DateOnly(1, 1, 2));
        RuleFor(x => x.ToDate).Must(date => date is null || date <= new DateOnly(9999, 12, 30));
        RuleFor(x => x).Must(query => query.FromDate is null || query.ToDate is null || query.FromDate <= query.ToDate)
            .WithMessage("Từ ngày không được sau đến ngày.");
    }
}
