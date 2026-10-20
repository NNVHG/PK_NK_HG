using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
namespace Dental.Application.Interfaces;

public interface IDentalImageClinicalRepository
{
    Task<Result<ImageClinicalContext>> ReadAsync(int image, int actor, string role, CancellationToken ct);
    Task<Result<PagedResult<ImageClinicalAddendumDto>>> HistoryAsync(int image, int actor, string role, int page, int pageSize, CancellationToken ct);
    Task<Result<ImageClinicalContext>> ReplaceAsync(int image, int actor, string role, ImageClinicalWriteRequest request, string? ip, CancellationToken ct);
    Task<Result<ImageClinicalAddendumDto>> AppendAsync(int image, int actor, string role, ImageClinicalAppendRequest request, string? ip, CancellationToken ct);
}
