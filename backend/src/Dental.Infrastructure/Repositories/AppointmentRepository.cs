using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class AppointmentRepository : IAppointmentRepository
{
    private readonly DentalDbContext _db;

    public AppointmentRepository(DentalDbContext db) => _db = db;

    public async Task AddAsync(Appointment appointment, CancellationToken ct = default)
        => await _db.Appointments.AddAsync(appointment, ct);

    public Task<Appointment?> GetByIdAsync(int appointmentId, CancellationToken ct = default)
        => _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Dentist)
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

    public Task<int> GetSlotBookingCountAsync(DateOnly date, TimeOnly slotTime, CancellationToken ct = default)
        => _db.Appointments
            .CountAsync(a => a.AppointmentDate == date &&
                             a.SlotTime == slotTime &&
                             a.Status == AppointmentStatuses.Scheduled, ct);

    public Task<bool> HasActiveAppointmentOnDateAsync(int patientId, DateOnly date, CancellationToken ct = default)
        => _db.Appointments
            .AnyAsync(a => a.PatientId == patientId &&
                           a.AppointmentDate == date &&
                           a.Status == AppointmentStatuses.Scheduled, ct);

    public async Task<(IReadOnlyList<Appointment> Items, int TotalCount)> GetAppointmentsAsync(
        DateOnly? date,
        int? patientId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Dentist)
            .Include(a => a.CreatedByUser)
            .AsQueryable();

        if (date.HasValue)
            query = query.Where(a => a.AppointmentDate == date.Value);

        if (patientId.HasValue)
            query = query.Where(a => a.PatientId == patientId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(a => a.AppointmentDate)
            .ThenBy(a => a.SlotTime)
            .ThenBy(a => a.AppointmentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
