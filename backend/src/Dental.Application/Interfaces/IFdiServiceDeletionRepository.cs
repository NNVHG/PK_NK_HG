using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
namespace Dental.Application.Interfaces;
public interface IFdiServiceDeletionRepository
{
    Task<Result> DeleteAsync(DeleteVisitServiceRequest request, int actorId, string role, string? ip, CancellationToken ct = default);
}
