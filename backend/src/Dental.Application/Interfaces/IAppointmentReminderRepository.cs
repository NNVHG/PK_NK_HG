using Dental.Domain.Entities;
namespace Dental.Application.Interfaces;

public interface IAppointmentReminderRepository
{
    Task<IReadOnlyList<Appointment>> GetCandidatesAsync(DateOnly start, DateOnly end, CancellationToken ct = default);
    Task<bool> HasOccurrenceAsync(int id, DateOnly date, TimeOnly time, CancellationToken ct = default);
    Task AddAsync(NotificationLog log, CancellationToken ct = default);
    Task<(IReadOnlyList<NotificationLog> Items, int Total)> GetLogsAsync(int? appointmentId, int page, int pageSize, CancellationToken ct = default);
}
