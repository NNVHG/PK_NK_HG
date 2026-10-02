using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class VisitRepository : IVisitRepository
{
    private readonly DentalDbContext _db;

    public VisitRepository(DentalDbContext db) => _db = db;

    public Task<Visit?> GetByIdAsync(int visitId, CancellationToken ct = default)
        => _db.Visits.AsNoTracking().FirstOrDefaultAsync(visit => visit.VisitId == visitId, ct);

    public Task<bool> HasOpenVisitAsync(int patientId, CancellationToken ct = default)
        => _db.Visits.AnyAsync(visit =>
            visit.PatientId == patientId &&
            (visit.Status == VisitStatuses.Created || visit.Status == VisitStatuses.InProgress), ct);

    public async Task AddAsync(Visit visit, CancellationToken ct = default)
        => await _db.Visits.AddAsync(visit, ct);

    public async Task<PagedResult<Visit>> GetPatientVisitsAsync(
        int patientId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Visits.AsNoTracking().Where(visit => visit.PatientId == patientId);
        var totalCount = await query.CountAsync(ct);
        var visits = await query
            .OrderByDescending(visit => visit.CreatedAt)
            .ThenByDescending(visit => visit.VisitId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<Visit>.Create(visits, totalCount, page, pageSize);
    }

    public async Task<PagedResult<PatientTimelineItemResponse>> GetPatientTimelineAsync(
        int patientId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Visits.AsNoTracking().Where(visit => visit.PatientId == patientId);
        var totalCount = await query.CountAsync(ct);

        // Phép chiếu gồm tên nha sĩ và hai EXISTS tương quan, nên dữ liệu cả trang được lấy trong một truy vấn, không N+1.
        var items = await query
            .OrderByDescending(visit => visit.CreatedAt)
            .ThenByDescending(visit => visit.VisitId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(visit => new PatientTimelineItemResponse(
                visit.VisitId,
                visit.Status,
                visit.StartedAt,
                visit.EndedAt,
                visit.Dentist == null ? null : visit.Dentist.FullName,
                _db.MedicalHistoryRecords.Any(record => record.VisitId == visit.VisitId),
                _db.VitalSignRecords.Any(record => record.VisitId == visit.VisitId)))
            .ToListAsync(ct);

        return PagedResult<PatientTimelineItemResponse>.Create(items, totalCount, page, pageSize);
    }
}
