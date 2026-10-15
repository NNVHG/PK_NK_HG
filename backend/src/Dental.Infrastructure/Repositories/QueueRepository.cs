using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class QueueRepository : IQueueRepository
{
    private readonly DentalDbContext _db;

    public QueueRepository(DentalDbContext db) => _db = db;

    public async Task AddAsync(QueueEntry entry, CancellationToken ct = default)
        => await _db.QueueEntries.AddAsync(entry, ct);

    public Task<QueueEntry?> GetByIdAsync(int queueEntryId, CancellationToken ct = default)
        => _db.QueueEntries
            .Include(q => q.Patient)
            .Include(q => q.Appointment)
            .Include(q => q.Dentist)
            .Include(q => q.Visit)
            .Include(q => q.StatusHistories.OrderBy(h => h.ChangedAt))
                .ThenInclude(h => h.ChangedByUser)
            .FirstOrDefaultAsync(q => q.QueueEntryId == queueEntryId, ct);

    public async Task<int> GetNextQueueNumberAsync(DateOnly date, CancellationToken ct = default)
    {
        var max = await _db.QueueEntries
            .Where(q => q.QueueDate == date)
            .MaxAsync(q => (int?)q.QueueNumber, ct);
        return (max ?? 0) + 1;
    }

    public async Task<IReadOnlyList<QueueEntry>> GetOpenQueueEntriesForPatientAsync(int patientId, CancellationToken ct = default)
    {
        return await _db.QueueEntries
            .Include(q => q.Visit)
            .Where(q => q.PatientId == patientId &&
                        (q.Status == QueueStatus.Waiting ||
                         q.Status == QueueStatus.Calling ||
                         q.Status == QueueStatus.InConsultation ||
                         q.Status == QueueStatus.InImaging))
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<QueueEntry> Items, int TotalCount)> GetTodayQueueAsync(
        DateOnly date,
        QueueStatus? status,
        int? dentistId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.QueueEntries
            .AsNoTracking()
            .Include(q => q.Patient)
            .Include(q => q.Dentist)
            .Include(q => q.Appointment)
            .Where(q => q.QueueDate == date);

        if (status.HasValue)
            query = query.Where(q => q.Status == status.Value);

        if (dentistId.HasValue)
            query = query.Where(q => q.DentistId == dentistId.Value);

        var totalCount = await query.CountAsync(ct);

        // DL-046/054: đúng hẹn theo giờ hẹn; thường/trễ hẹn theo giờ check-in.
        var items = await query
            .OrderByDescending(q => q.Status == QueueStatus.Waiting && q.StatusHistories.Any(h =>
                h.FromStatus == QueueStatus.InImaging && h.ToStatus == QueueStatus.Waiting))
            .ThenByDescending(q => q.IsPriority)
            .ThenBy(q => q.IsPriority && q.Appointment != null ? q.Appointment.SlotTime : TimeOnly.MaxValue)
            .ThenBy(q => q.CheckInTime)
            .ThenBy(q => q.QueueNumber)
            .ThenBy(q => q.QueueEntryId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<bool> HasCallingForDentistAsync(DateOnly date, int dentistId, CancellationToken ct = default, int? excludedQueueEntryId = null)
        => _db.QueueEntries.AnyAsync(q => q.QueueDate == date && q.DentistId == dentistId && q.Status == QueueStatus.Calling &&
            (!excludedQueueEntryId.HasValue || q.QueueEntryId != excludedQueueEntryId.Value), ct);

    public async Task<QueueEntry?> GetNextWaitingEntryAsync(DateOnly date, int? dentistId, CancellationToken ct = default, bool assignedOnly = false)
    {
        var query = _db.QueueEntries
            .Include(q => q.Patient)
            .Include(q => q.Dentist)
            .Include(q => q.Appointment)
            .Include(q => q.Visit)
            .Where(q => q.QueueDate == date && q.Status == QueueStatus.Waiting);

        if (dentistId.HasValue)
        {
            query = query.Where(q => q.DentistId == dentistId.Value || (!assignedOnly && q.DentistId == null));
        }

        return await query
            .OrderByDescending(q => q.StatusHistories.Any(h =>
                h.FromStatus == QueueStatus.InImaging && h.ToStatus == QueueStatus.Waiting))
            .ThenByDescending(q => q.IsPriority)
            .ThenBy(q => q.IsPriority && q.Appointment != null ? q.Appointment.SlotTime : TimeOnly.MaxValue)
            .ThenBy(q => q.CheckInTime)
            .ThenBy(q => q.QueueNumber)
            .ThenBy(q => q.QueueEntryId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<QueueEntry>> GetPublicDisplayQueueAsync(DateOnly date, CancellationToken ct = default)
    {
        return await _db.QueueEntries
            .AsNoTracking()
            .Include(q => q.Patient)
            .Include(q => q.Dentist)
            .Include(q => q.Appointment)
            .Include(q => q.StatusHistories.Where(h => h.FromStatus == QueueStatus.InImaging && h.ToStatus == QueueStatus.Waiting))
            .Where(q => q.QueueDate == date &&
                        (q.Status == QueueStatus.InConsultation || q.Status == QueueStatus.Calling || q.Status == QueueStatus.Waiting))
            .ToListAsync(ct);
    }

    public async Task AddStatusHistoryAsync(QueueStatusHistory history, CancellationToken ct = default)
        => await _db.QueueStatusHistories.AddAsync(history, ct);

    public async Task<IReadOnlyList<QueueStatusHistory>> GetStatusHistoriesAsync(int queueEntryId, CancellationToken ct = default)
    {
        return await _db.QueueStatusHistories
            .AsNoTracking()
            .Include(h => h.ChangedByUser)
            .Where(h => h.QueueEntryId == queueEntryId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(ct);
    }
}
