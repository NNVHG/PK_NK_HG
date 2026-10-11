using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
namespace Dental.Application.Interfaces;
public interface IReceiptRepository
{
    Task<Result<ReceiptPrintDto>> GetAsync(ReceiptRequest request, CancellationToken ct = default);
    Task<Result<ReceiptPrintDto>> PrintAsync(ReceiptRequest request, int actorId, string? ip, CancellationToken ct = default);
}
