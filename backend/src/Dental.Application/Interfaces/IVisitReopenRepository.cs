using Dental.Application.Common;
using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IVisitReopenRepository
{
    Task<IReadOnlyList<VisitUnlockRecord>> GetHistoryAsync(int visitId, CancellationToken ct = default);
    // Includes immutable reason record and audit in the same transaction as Visit/Invoice/Queue.
    Task<Result<VisitUnlockRecord>> ReopenAsync(int visitId, int actor, string reason, string? ip, CancellationToken ct = default);
}
