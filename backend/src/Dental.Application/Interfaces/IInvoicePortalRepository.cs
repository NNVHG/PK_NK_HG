using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
namespace Dental.Application.Interfaces;
public interface IInvoicePortalRepository
{
    Task<PagedResult<InvoicePortalItemResponse>?> GetItemsAsync(int invoiceId, int patientId, int userId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<InvoicePortalProfileResponse>> GetProfilesAsync(int userId, CancellationToken ct = default);
    Task<bool> OwnsPatientAsync(int patientId, int userId, CancellationToken ct = default);
    Task<PagedResult<InvoiceLookupResponse>> GetInvoicesAsync(int patientId, int userId, int page, int pageSize, int? status = null, DateTime? fromUtc = null, DateTime? untilUtc = null, CancellationToken ct = default);
    Task<PagedResult<InvoicePortalPaymentResponse>?> GetPaymentsAsync(int invoiceId, int patientId, int userId, int page, int pageSize, CancellationToken ct = default);
}
