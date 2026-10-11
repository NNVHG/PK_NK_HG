using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
namespace Dental.Application.Interfaces;
public interface IToothHistoryRepository
{
    Task<PagedResult<ToothHistoryItemResponse>> GetAsync(ToothHistoryRequest request, CancellationToken ct = default);
}
