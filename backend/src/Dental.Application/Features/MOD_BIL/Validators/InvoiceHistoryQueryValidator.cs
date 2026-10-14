using System.Globalization;
using Dental.Application.Features.MOD_BIL.DTOs;
using FluentValidation;
namespace Dental.Application.Features.MOD_BIL.Validators;
public sealed class InvoiceHistoryQueryValidator : AbstractValidator<InvoiceHistoryQuery>
{
    public InvoiceHistoryQueryValidator()
    {
        RuleFor(x => x.PatientCode).Must(code => TryParsePatientCode(code, out _))
            .WithMessage("Nhập mã bệnh nhân đầy đủ theo định dạng BN000001.");
        RuleFor(x => x.Status).Must(status => status is null or >= 0 and <= 4);
        // Boundaries prevent UTC conversion or the exclusive next-day bound from overflowing DateTime.
        RuleFor(x => x.FromDate).Must(date => date is null || date >= new DateOnly(1, 1, 2));
        RuleFor(x => x.ToDate).Must(date => date is null || date <= new DateOnly(9999, 12, 30));
        RuleFor(x => x).Must(query => query.FromDate is null || query.ToDate is null || query.FromDate <= query.ToDate)
            .WithMessage("Từ ngày không được sau đến ngày.");
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
    public static bool TryParsePatientCode(string? code, out int? number)
    {
        number = null;
        if (string.IsNullOrWhiteSpace(code)) return true;
        var value = code.Trim().ToUpperInvariant();
        if (!value.StartsWith("BN", StringComparison.Ordinal) ||
            !int.TryParse(value.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0 ||
            value != "BN" + parsed.ToString("D6", CultureInfo.InvariantCulture)) return false;
        number = parsed; return true;
    }
}
