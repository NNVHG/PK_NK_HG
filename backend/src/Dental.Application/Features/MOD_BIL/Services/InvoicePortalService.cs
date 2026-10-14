using System.Globalization;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
namespace Dental.Application.Features.MOD_BIL.Services;
public sealed class InvoicePortalService(IInvoicePortalRepository repository, InvoicePortalQueryValidator validator)
{
    public async Task<Result<PagedResult<InvoiceLookupResponse>>> ListAsync(InvoicePortalQuery request, int userId, string? role, CancellationToken ct = default)
    {
        var scope = await ValidateScopeAsync(request, userId, role, ct);
        if (scope.IsFailure) return Result<PagedResult<InvoiceLookupResponse>>.Failure(scope.Error);
        return Result<PagedResult<InvoiceLookupResponse>>.Success(await repository.GetInvoicesAsync(scope.Value!, userId, request.Page, request.PageSize, ct));
    }
    public async Task<Result<PagedResult<InvoicePortalPaymentResponse>>> PaymentsAsync(int invoiceId, InvoicePortalQuery request, int userId, string? role, CancellationToken ct = default)
    {
        var scope = await ValidateScopeAsync(request, userId, role, ct);
        if (scope.IsFailure) return Result<PagedResult<InvoicePortalPaymentResponse>>.Failure(scope.Error);
        if (invoiceId <= 0) return Result<PagedResult<InvoicePortalPaymentResponse>>.Failure(Error.Validation);
        var rows = await repository.GetPaymentsAsync(invoiceId, scope.Value!, userId, request.Page, request.PageSize, ct);
        return rows is null ? Result<PagedResult<InvoicePortalPaymentResponse>>.Failure(Error.Forbidden)
            : Result<PagedResult<InvoicePortalPaymentResponse>>.Success(rows);
    }
    public async Task<Result<PagedResult<InvoicePortalItemResponse>>> ItemsAsync(int invoiceId, InvoicePortalQuery request, int userId, string? role, CancellationToken ct = default)
    {
        var scope = await ValidateScopeAsync(request, userId, role, ct);
        if (scope.IsFailure) return Result<PagedResult<InvoicePortalItemResponse>>.Failure(scope.Error);
        if (invoiceId <= 0) return Result<PagedResult<InvoicePortalItemResponse>>.Failure(Error.Validation);
        var rows = await repository.GetItemsAsync(invoiceId, scope.Value!, userId, request.Page, request.PageSize, ct);
        return rows is null ? Result<PagedResult<InvoicePortalItemResponse>>.Failure(Error.Forbidden)
            : Result<PagedResult<InvoicePortalItemResponse>>.Success(rows);
    }
    private async Task<Result<int>> ValidateScopeAsync(InvoicePortalQuery request, int userId, string? role, CancellationToken ct)
    {
        if (role != RoleCodes.Patient || userId <= 0 ||
            !int.TryParse(request.PatientId, NumberStyles.None, CultureInfo.InvariantCulture, out var patientId) || patientId <= 0)
            return Result<int>.Failure(Error.Forbidden);
        if (!(await validator.ValidateAsync(request, ct)).IsValid) return Result<int>.Failure(Error.Validation);
        if (!await repository.OwnsPatientAsync(patientId, userId, ct)) return Result<int>.Failure(Error.Forbidden);
        return Result<int>.Success(patientId);
    }
}
