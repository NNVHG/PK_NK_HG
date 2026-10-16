using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class PostTreatmentNoteRepository : IPostTreatmentNoteRepository
{
    private readonly DentalDbContext _db;

    public PostTreatmentNoteRepository(DentalDbContext db)
    {
        _db = db;
    }

    public Task<PostTreatmentNote?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.PostTreatmentNotes
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<PostTreatmentNote?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default)
        => _db.PostTreatmentNotes
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(x => x.RequestId == requestId, ct);

    public async Task<IReadOnlyList<PostTreatmentNote>> GetByVisitIdAsync(int visitId, CancellationToken ct = default)
    {
        return await _db.PostTreatmentNotes
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
                .ThenInclude(u => u.Role)
            .Where(x => x.VisitId == visitId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(PostTreatmentNote note, CancellationToken ct = default)
    {
        await _db.PostTreatmentNotes.AddAsync(note, ct);
    }
}
