using Dental.Application.Common;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class QueueRepository : IQueueRepository
{
    private readonly DentalDbContext _db;
    private readonly VietnamClock _clock;

    public QueueRepository(DentalDbContext db, VietnamClock? clock = null)
    {
        _db = db;
        _clock = clock ?? new VietnamClock(TimeProvider.System);
    }

    public async Task<IReadOnlyList<Dental.Application.Features.Queue.DTOs.QueueDentistLocation>> GetDentistLocationsAsync(
        IReadOnlyCollection<int> dentistIds, CancellationToken ct = default)
    {
        if (dentistIds.Count == 0) return [];
        var today = _clock.Today;
        return await (from dentist in _db.Users.AsNoTracking()
                      join room in _db.ClinicRooms on dentist.WorkspaceRoomId equals (int?)room.Id
                      where dentistIds.Contains(dentist.UserId) && dentist.WorkspaceDate == today &&
                          dentist.IsActive &&
                          dentist.Role.RoleCode == Dental.Domain.Constants.RoleCodes.Dentist && room.IsActive &&
                          (room.Status == DentalChairStatus.Available || room.Status == DentalChairStatus.Occupied)
                      select new Dental.Application.Features.Queue.DTOs.QueueDentistLocation(
                          dentist.UserId, room.RoomCode, room.RoomName)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<QueueEntry>> GetExpiredUnstartedAsync(DateOnly lastExpiredDate, CancellationToken ct = default)
        => await _db.QueueEntries
            .Include(q => q.Patient)
            .Include(q => q.Appointment)
            .Include(q => q.Visit)
            .Where(q => q.QueueDate <= lastExpiredDate &&
                (q.Status == QueueStatus.Waiting || q.Status == QueueStatus.Calling || q.Status == QueueStatus.Skipped) &&
                q.Visit != null && q.Visit.Status == Dental.Domain.Constants.VisitStatuses.Created && q.Visit.StartedAt == null && !q.Visit.IsLocked)
            .OrderBy(q => q.QueueDate).ThenBy(q => q.QueueEntryId).ToListAsync(ct);

    public async Task AddAsync(QueueEntry entry, CancellationToken ct = default)
        => await _db.QueueEntries.AddAsync(entry, ct);

    public async Task<QueueEntry?> GetByIdAsync(int queueEntryId, CancellationToken ct = default)
    {
        var entry = await _db.QueueEntries
            .Include(q => q.Patient)
            .Include(q => q.Appointment)
            .Include(q => q.Dentist)
            .Include(q => q.Visit)
            .FirstOrDefaultAsync(q => q.QueueEntryId == queueEntryId, ct);
        if (entry is not null)
        {
            var histories = await HydrateHistoriesAsync(entry.StatusHistoryJson, ct);
            foreach (var h in histories) h.QueueEntry = entry;
            entry.StatusHistories = histories;
        }
        return entry;
    }

    public async Task<int> GetNextQueueNumberAsync(DateOnly date, CancellationToken ct = default)
    {
        var max = await _db.QueueEntries
            .Where(q => q.QueueDate == date)
            .MaxAsync(q => (int?)q.QueueNumber, ct);
        return (max ?? 0) + 1;
    }

    public async Task<IReadOnlyList<QueueEntry>> GetOpenQueueEntriesForPatientAsync(int patientId, CancellationToken ct = default)
        => await OpenEntries().Where(q => q.PatientId == patientId).ToListAsync(ct);

    public async Task<IReadOnlyList<QueueEntry>> GetOpenQueueEntriesForPatientsAsync(int[] patientIds, CancellationToken ct = default)
        => await OpenEntries().AsNoTracking().Where(q => patientIds.Contains(q.PatientId)).ToListAsync(ct);

    private IQueryable<QueueEntry> OpenEntries() => _db.QueueEntries.Include(q => q.Visit)
        .Where(q => q.Status == QueueStatus.Waiting || q.Status == QueueStatus.Calling ||
                    q.Status == QueueStatus.Skipped || q.Status == QueueStatus.InConsultation ||
                    q.Status == QueueStatus.InImaging || q.Status == QueueStatus.Paused);

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
            .Include(q => q.Visit)
            .Where(q => q.QueueDate == date);

        if (status.HasValue)
            query = query.Where(q => q.Status == status.Value);

        if (dentistId.HasValue)
            query = query.Where(q => q.DentistId == dentistId.Value);

        var totalCount = await query.CountAsync(ct);

        // DL-046/054: đúng hẹn theo giờ hẹn; thường/trễ hẹn theo giờ check-in.
        var items = await query
            .OrderByDescending(q => q.Status == QueueStatus.Waiting && q.ReturnedFromImaging)
            .ThenByDescending(q => q.Status == QueueStatus.Waiting && q.ReturnedFromSkipped)
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
            .OrderByDescending(q => q.ReturnedFromImaging)
            .ThenByDescending(q => q.ReturnedFromSkipped)
            .ThenByDescending(q => q.IsPriority)
            .ThenBy(q => q.IsPriority && q.Appointment != null ? q.Appointment.SlotTime : TimeOnly.MaxValue)
            .ThenBy(q => q.CheckInTime)
            .ThenBy(q => q.QueueNumber)
            .ThenBy(q => q.QueueEntryId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<QueueStatusHistory>> GetActivePublicCallsAsync(
        DateOnly date, DateTime windowStartUtc, DateTime utcNow, CancellationToken ct = default)
    {
        var entries = await _db.QueueEntries
            .AsNoTracking()
            .Include(q => q.Patient)
            .Where(q => q.QueueDate == date && q.Status == QueueStatus.Calling &&
                q.PublicCalledAtUtc != null &&
                q.PublicCalledAtUtc > windowStartUtc && q.PublicCalledAtUtc <= utcNow &&
                q.PublicCalledByUser != null && q.PublicCalledByUser.IsActive &&
                q.PublicCalledByUser.Role.RoleCode == RoleCodes.Dentist &&
                q.PublicCalledByUserId == q.DentistId)
            .OrderByDescending(q => q.PublicCalledAtUtc)
            .ThenByDescending(q => q.QueueEntryId)
            .Take(5)
            .ToListAsync(ct);

        return entries.Select(q => new QueueStatusHistory
        {
            QueueEntryId = q.QueueEntryId,
            QueueEntry = q,
            FromStatus = QueueStatus.Waiting,
            ToStatus = QueueStatus.Calling,
            ChangedByUserId = q.PublicCalledByUserId,
            ChangedAt = q.PublicCalledAtUtc!.Value,
            Reason = QueueStatusHistory.PublicInvitationReason
        }).ToList();
    }

    public async Task AddStatusHistoryAsync(QueueStatusHistory history, CancellationToken ct = default)
    {
        var entry = _db.QueueEntries.Local.FirstOrDefault(q => q.QueueEntryId == history.QueueEntryId)
            ?? await _db.QueueEntries.FirstAsync(q => q.QueueEntryId == history.QueueEntryId, ct);
        entry.AppendStatusHistory(history);
    }

    public async Task<IReadOnlyList<QueueStatusHistory>> GetStatusHistoriesAsync(int queueEntryId, CancellationToken ct = default)
    {
        var json = await _db.QueueEntries.AsNoTracking()
            .Where(q => q.QueueEntryId == queueEntryId)
            .Select(q => q.StatusHistoryJson)
            .FirstOrDefaultAsync(ct);
        return await HydrateHistoriesAsync(json, ct);
    }

    private async Task<List<QueueStatusHistory>> HydrateHistoriesAsync(string? json, CancellationToken ct)
    {
        var items = string.IsNullOrEmpty(json)
            ? new List<QueueStatusHistory>()
            : System.Text.Json.JsonSerializer.Deserialize<List<QueueStatusHistory>>(json) ?? new List<QueueStatusHistory>();
        if (items.Count == 0) return items;
        var userIds = items.Where(h => h.ChangedByUserId.HasValue).Select(h => h.ChangedByUserId!.Value).Distinct().ToArray();
        if (userIds.Length > 0)
        {
            var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, ct);
            foreach (var h in items)
                if (h.ChangedByUserId.HasValue && users.TryGetValue(h.ChangedByUserId.Value, out var u))
                    h.ChangedByUser = u;
        }
        return items.OrderBy(h => h.ChangedAt).ToList();
    }
}
