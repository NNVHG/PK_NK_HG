using Dental.Application.Features.MOD_BIL.DTOs;
namespace Dental.Application.Interfaces;
public interface IInvoiceLookupRepository
{
    Task<InvoiceLookupResponse?> FindByCodeAsync(string normalizedCode, CancellationToken ct = default);
}
