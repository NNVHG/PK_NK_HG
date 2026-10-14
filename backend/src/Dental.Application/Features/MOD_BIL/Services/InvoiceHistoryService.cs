using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
namespace Dental.Application.Features.MOD_BIL.Services;
public sealed class InvoiceHistoryService(IInvoiceHistoryRepository repository, InvoiceHistoryQueryValidator validator)
{
    public async Task<Result<PagedResult<InvoiceLookupResponse>>> GetAsync(InvoiceHistoryQuery request, string? role, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist)) return Result<PagedResult<InvoiceLookupResponse>>.Failure(Error.Forbidden);
        if (!(await validator.ValidateAsync(request, ct)).IsValid) return Result<PagedResult<InvoiceLookupResponse>>.Failure(Error.Validation);
        InvoiceHistoryQueryValidator.TryParsePatientCode(request.PatientCode, out var number);
        // Invoice list dates use Vietnam's UTC+7 calendar, including the entire ToDate.
        DateTime? fromUtc = request.FromDate is DateOnly from
            ? DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc) : null;
        DateTime? untilUtc = request.ToDate is DateOnly to
            ? DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc) : null;
        return Result<PagedResult<InvoiceLookupResponse>>.Success(
            await repository.GetPageAsync(number, request.Status, request.Page, request.PageSize, fromUtc, untilUtc, string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim(), ct));
    }
}
