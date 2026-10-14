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
        return Result<PagedResult<InvoiceLookupResponse>>.Success(
            await repository.GetPageAsync(number, request.Status, request.Page, request.PageSize, ct));
    }
}
