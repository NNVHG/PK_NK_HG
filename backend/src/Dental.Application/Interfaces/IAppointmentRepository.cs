using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IAppointmentRepository
{
    Task AddAsync(Appointment appointment, CancellationToken ct = default);
    Task<Appointment?> GetByIdAsync(int appointmentId, CancellationToken ct = default);
    Task<int> GetSlotBookingCountAsync(DateOnly date, TimeOnly slotTime, CancellationToken ct = default);
    Task<bool> HasActiveAppointmentOnDateAsync(int patientId, DateOnly date, CancellationToken ct = default);
    Task<(IReadOnlyList<Appointment> Items, int TotalCount)> GetAppointmentsAsync(
        DateOnly? date,
        int? patientId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
