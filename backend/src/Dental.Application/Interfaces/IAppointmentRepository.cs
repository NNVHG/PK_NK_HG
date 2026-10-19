using Dental.Domain.Entities;
using Dental.Application.Features.MOD_MST.Services;

namespace Dental.Application.Interfaces;

public interface IAppointmentRepository
{
    Task AddAsync(Appointment appointment, CancellationToken ct = default);
    Task<Appointment?> GetByIdAsync(int appointmentId, CancellationToken ct = default);
    Task<ScheduleSnapshot?> GetScheduleAsync(CancellationToken ct = default);
    Task<int> GetSlotBookingCountAsync(DateOnly date, TimeOnly slotTime, CancellationToken ct = default, int? excludedId = null, int? durationMinutes = null);
    Task<bool> HasActiveAppointmentOnDateAsync(int patientId, DateOnly date, CancellationToken ct = default, int? excludedId = null);
    Task<bool> HasDentistBookingAsync(int dentistId, DateOnly date, TimeOnly slotTime, int excludedId, CancellationToken ct = default, int? durationMinutes = null);
    Task<Appointment?> GetDentistBookingConflictAsync(int dentistId, DateOnly date, TimeOnly slotTime, int excludedId, CancellationToken ct = default, int? durationMinutes = null);
    Task<(IReadOnlyList<Appointment> Items, int TotalCount)> GetAppointmentsAsync(
        DateOnly? date,
        int? patientId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default,
        int? ownerUserId = null);
}
