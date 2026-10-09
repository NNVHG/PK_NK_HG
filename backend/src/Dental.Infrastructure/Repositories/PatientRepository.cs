using Dental.Application.Common;
using Dental.Application.Features.Patient;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class PatientRepository : IPatientRepository
{
    private readonly DentalDbContext _db;

    public PatientRepository(DentalDbContext db) => _db = db;

    public Task<Patient?> GetByIdAsync(int patientId, CancellationToken ct = default)
        => _db.Patients.FirstOrDefaultAsync(patient => patient.PatientId == patientId, ct);

    public Task<Patient?> GetByUserIdAsync(int userId, CancellationToken ct = default)
        => _db.Patients.FirstOrDefaultAsync(patient => patient.UserId == userId, ct);

    public async Task<IReadOnlyList<Patient>> GetActiveByUserIdAsync(int userId, CancellationToken ct = default)
        => await _db.Patients
            .AsNoTracking()
            .Where(patient => patient.UserId == userId && patient.IsActive)
            .OrderBy(patient => patient.FullName)
            .ThenBy(patient => patient.PatientNumber)
            .ToListAsync(ct);

    public async Task<PagedResult<Patient>> SearchAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Patients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            if (PatientCodeParser.TryParseSearchRange(term, out var minimum, out var maximum))
            {
                query = query.Where(patient => patient.PatientNumber >= minimum && patient.PatientNumber <= maximum);
            }
            else
            {
                // [CẦN XÁC NHẬN] Tìm họ tên dùng ILIKE; tìm không phân biệt dấu cần extension unaccent ngoài phạm vi.
                var pattern = $"%{term}%";
                query = query.Where(patient =>
                    EF.Functions.ILike(patient.FullName, pattern)
                    || EF.Functions.ILike(patient.Phone, pattern));
            }
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(patient => patient.FullName)
            .ThenBy(patient => patient.PatientNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<Patient>.Create(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<Patient>> FindPossibleDuplicatesAsync(
        string phone,
        string fullName,
        DateOnly dateOfBirth,
        CancellationToken ct = default,
        int? excludePatientId = null)
    {
        var normalizedPhone = phone.Trim();
        var normalizedFullName = fullName.Trim().ToLowerInvariant();

        return await _db.Patients
            .AsNoTracking()
            .Where(patient => (!excludePatientId.HasValue || patient.PatientId != excludePatientId.Value)
                              && patient.Phone == normalizedPhone
                              && (patient.DateOfBirth == dateOfBirth
                                  || patient.FullName.ToLower() == normalizedFullName))
            .OrderBy(patient => patient.PatientNumber)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Patient patient, CancellationToken ct = default)
        => await _db.Patients.AddAsync(patient, ct);
}
