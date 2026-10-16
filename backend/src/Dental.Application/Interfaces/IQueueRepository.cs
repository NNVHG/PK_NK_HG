using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Interfaces;

public interface IQueueRepository
{
    Task<IReadOnlyList<Dental.Application.Features.Queue.DTOs.QueueDentistLocation>> GetDentistLocationsAsync(
        IReadOnlyCollection<int> dentistIds, CancellationToken ct = default);
    Task<IReadOnlyList<QueueEntry>> GetExpiredUnstartedAsync(DateOnly lastExpiredDate, CancellationToken ct = default);
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
    Task<QueueEntry?> GetNextWaitingEntryAsync(DateOnly date, int? dentistId, CancellationToken ct = default, bool assignedOnly = false);
    Task<bool> HasCallingForDentistAsync(DateOnly date, int dentistId, CancellationToken ct = default, int? excludedQueueEntryId = null);
    Task<IReadOnlyList<QueueEntry>> GetPublicDisplayQueueAsync(DateOnly date, CancellationToken ct = default);
    Task AddStatusHistoryAsync(QueueStatusHistory history, CancellationToken ct = default);
    Task<IReadOnlyList<QueueStatusHistory>> GetStatusHistoriesAsync(int queueEntryId, CancellationToken ct = default);
}
