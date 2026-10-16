using Dental.Application.Common;
using Dental.Domain.Entities;
namespace Dental.Application.Interfaces;
public interface IVisitChairCoordinator
{
    Task<Result> CheckReadyAsync(Visit visit, int dentistId, CancellationToken ct = default);
    Task<Result> AcquireAsync(Visit visit, int dentistId, int actorId, CancellationToken ct = default);
    Task<Result> ReleaseAsync(Visit visit, int actorId, CancellationToken ct = default);
}
