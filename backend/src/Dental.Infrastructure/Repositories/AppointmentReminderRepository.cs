using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Repositories;

public sealed class AppointmentReminderRepository(DentalDbContext db) : IAppointmentReminderRepository
{
    public async Task<IReadOnlyList<Appointment>> GetCandidatesAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
        => await db.Appointments.AsNoTracking().Include(a => a.Patient).Include(a => a.Dentist)
            .Where(a => a.Status == AppointmentStatuses.Scheduled && a.AppointmentDate >= start && a.AppointmentDate <= end)
            .OrderBy(a => a.AppointmentDate).ThenBy(a => a.SlotTime).ThenBy(a => a.AppointmentId).ToListAsync(ct);
    public Task<bool> HasOccurrenceAsync(int id, DateOnly date, TimeOnly time, CancellationToken ct = default)
        => db.NotificationLogs.AnyAsync(l => l.AppointmentId == id && l.NotificationType == "APPOINTMENT_REMINDER_24H" && l.AppointmentDate == date && l.SlotTime == time, ct);
    public async Task AddAsync(NotificationLog log, CancellationToken ct = default) => await db.NotificationLogs.AddAsync(log, ct);
    public async Task<(IReadOnlyList<NotificationLog> Items, int Total)> GetLogsAsync(int? appointmentId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.NotificationLogs.AsNoTracking().AsQueryable();
        if (appointmentId.HasValue) query = query.Where(l => l.AppointmentId == appointmentId.Value);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(l => l.SentAtUtc).ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (rows, count);
    }
}
