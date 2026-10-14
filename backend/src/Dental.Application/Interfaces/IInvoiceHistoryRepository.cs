using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
namespace Dental.Application.Interfaces;
public interface IInvoiceHistoryRepository
{
    Task<PagedResult<InvoiceLookupResponse>> GetPageAsync(int? patientNumber, int? status, int page, int pageSize, CancellationToken ct = default);
}
