using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Interfaces;

public interface IQueueRepository
{
    Task AddAsync(QueueEntry entry, CancellationToken ct = default);
    Task<QueueEntry?> GetByIdAsync(int queueEntryId, CancellationToken ct = default);
    Task<int> GetNextQueueNumberAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<QueueEntry>> GetOpenQueueEntriesForPatientAsync(int patientId, CancellationToken ct = default);
    Task<(IReadOnlyList<QueueEntry> Items, int TotalCount)> GetTodayQueueAsync(
        DateOnly date,
        QueueStatus? status,
        int? dentistId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task AddStatusHistoryAsync(QueueStatusHistory history, CancellationToken ct = default);
    Task<IReadOnlyList<QueueStatusHistory>> GetStatusHistoriesAsync(int queueEntryId, CancellationToken ct = default);
}
