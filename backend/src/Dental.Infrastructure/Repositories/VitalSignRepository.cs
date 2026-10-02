using Dental.Application.Common;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class VitalSignRepository : IVitalSignRepository
{
    private readonly DentalDbContext _db;

    public VitalSignRepository(DentalDbContext db) => _db = db;

    public async Task AddAsync(VitalSignRecord record, CancellationToken ct = default)
        => await _db.VitalSignRecords.AddAsync(record, ct);

    public async Task<PagedResult<VitalSignRecord>> GetPatientRecordsAsync(
        int patientId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.VitalSignRecords.AsNoTracking()
            .Where(record => record.PatientId == patientId);
        var totalCount = await query.CountAsync(ct);
        var records = await query
            .OrderByDescending(record => record.CreatedAt)
            .ThenByDescending(record => record.VitalSignRecordId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<VitalSignRecord>.Create(records, totalCount, page, pageSize);
    }
}
