using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IPostTreatmentNoteRepository
{
    Task<PostTreatmentNote?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PostTreatmentNote?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default);
    Task<IReadOnlyList<PostTreatmentNote>> GetByVisitIdAsync(int visitId, CancellationToken ct = default);
    Task AddAsync(PostTreatmentNote note, CancellationToken ct = default);
}
