using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
namespace Dental.Application.Features.MOD_BIL.Services;
public sealed class InvoiceLookupService(IInvoiceLookupRepository repository, InvoiceLookupRequestValidator validator)
{
    public async Task<Result<InvoiceLookupResponse>> GetAsync(InvoiceLookupRequest request, string? role, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist)) return Result<InvoiceLookupResponse>.Failure(Error.Forbidden);
        if (!(await validator.ValidateAsync(request, ct)).IsValid) return Result<InvoiceLookupResponse>.Failure(Error.Validation);
        var invoice = await repository.FindByCodeAsync(request.Code!.Trim().ToUpperInvariant(), ct);
        return invoice is null ? Result<InvoiceLookupResponse>.Failure(Error.NotFound) : Result<InvoiceLookupResponse>.Success(invoice);
    }
}
