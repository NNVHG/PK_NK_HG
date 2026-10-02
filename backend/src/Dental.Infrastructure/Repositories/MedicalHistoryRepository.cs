using Dental.Application.Common;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class MedicalHistoryRepository : IMedicalHistoryRepository
{
    private readonly DentalDbContext _db;

    public MedicalHistoryRepository(DentalDbContext db) => _db = db;

    public async Task AddAsync(MedicalHistoryRecord record, CancellationToken ct = default)
        => await _db.MedicalHistoryRecords.AddAsync(record, ct);

    public Task<MedicalHistoryRecord?> GetLatestByPatientIdAsync(int patientId, CancellationToken ct = default)
        => _db.MedicalHistoryRecords
            .AsNoTracking()
            .Include(record => record.Items)
            .Where(record => record.PatientId == patientId)
            .OrderByDescending(record => record.CreatedAt)
            .ThenByDescending(record => record.RecordId)
            .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<MedicalHistoryRecord>> GetPatientHistoryAsync(
        int patientId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.MedicalHistoryRecords
            .AsNoTracking()
            .Include(record => record.Items)
            .Where(record => record.PatientId == patientId);
        var totalCount = await query.CountAsync(ct);
        var records = await query
            .OrderByDescending(record => record.CreatedAt)
            .ThenByDescending(record => record.RecordId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<MedicalHistoryRecord>.Create(records, totalCount, page, pageSize);
    }
}
