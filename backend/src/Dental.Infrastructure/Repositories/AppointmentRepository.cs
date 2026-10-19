using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Services;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Infrastructure.Services;

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

    public async Task<ScheduleSnapshot?> GetScheduleAsync(CancellationToken ct = default) => await ClinicScheduleReader.ReadAsync(_db, ct);

    private IQueryable<Appointment> Nearby(DateOnly date, int? excludedId)
    {
        var first = DateOnly.FromDayNumber(Math.Max(DateOnly.MinValue.DayNumber, date.DayNumber - 1));
        var last = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, date.DayNumber + 1));
        return _db.Appointments.AsNoTracking().Where(a => a.AppointmentDate >= first && a.AppointmentDate <= last &&
            a.Status == AppointmentStatuses.Scheduled && (!excludedId.HasValue || a.AppointmentId != excludedId.Value));
    }
    public async Task<int> GetSlotBookingCountAsync(DateOnly date, TimeOnly slotTime, CancellationToken ct = default, int? excludedId = null, int? durationMinutes = null)
        => BookingOccupancy.Peak(await Nearby(date, excludedId).ToArrayAsync(ct), date, slotTime, durationMinutes ?? 30);

    public Task<bool> HasActiveAppointmentOnDateAsync(int patientId, DateOnly date, CancellationToken ct = default, int? excludedId = null)
        => _db.Appointments
            .AnyAsync(a => a.PatientId == patientId &&
                           a.AppointmentDate == date &&
                           a.Status == AppointmentStatuses.Scheduled && (!excludedId.HasValue || a.AppointmentId != excludedId.Value), ct);

    public async Task<bool> HasDentistBookingAsync(int dentistId, DateOnly date, TimeOnly slotTime, int excludedId, CancellationToken ct = default, int? durationMinutes = null)
        => await GetDentistBookingConflictAsync(dentistId, date, slotTime, excludedId, ct, durationMinutes) is not null;

    public async Task<Appointment?> GetDentistBookingConflictAsync(int dentistId, DateOnly date, TimeOnly slotTime, int excludedId, CancellationToken ct = default, int? durationMinutes = null)
        => (await Nearby(date, excludedId).Where(a => a.DentistId == dentistId)
            .Include(a => a.Patient)
            .Include(a => a.Dentist)
            .OrderBy(a => a.AppointmentDate).ThenBy(a => a.SlotTime).ThenBy(a => a.AppointmentId).ToArrayAsync(ct))
            .FirstOrDefault(a => BookingOccupancy.Overlaps(a, date, slotTime, durationMinutes ?? 30));

    public async Task<(IReadOnlyList<Appointment> Items, int TotalCount)> GetAppointmentsAsync(
        DateOnly? date,
        int? patientId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default,
        int? ownerUserId = null)
    {
        var query = _db.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Dentist)
            .Include(a => a.CreatedByUser)
            .AsQueryable();

        if (ownerUserId.HasValue)
            query = query.Where(a => a.Patient.UserId == ownerUserId.Value);

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
