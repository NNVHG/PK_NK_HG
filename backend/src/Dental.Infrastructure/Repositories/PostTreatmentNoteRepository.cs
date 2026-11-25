using System.Text.Json;
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

    private static List<PostTreatmentNote> ReadNotes(string? json)
        => string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<PostTreatmentNote>>(json) ?? [];

    private async Task<PostTreatmentNote> HydrateAuthorAsync(PostTreatmentNote note, CancellationToken ct)
    {
        var author = await _db.Users.AsNoTracking().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == note.CreatedByUserId, ct);
        if (author is not null) note.CreatedByUser = author;
        return note;
    }

    public async Task<PostTreatmentNote?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var jsons = await _db.Visits.AsNoTracking()
            .Where(v => v.PostTreatmentNotesJson != null)
            .Select(v => v.PostTreatmentNotesJson!)
            .ToListAsync(ct);
        foreach (var json in jsons)
        {
            var match = ReadNotes(json).FirstOrDefault(n => n.Id == id);
            if (match is not null) return await HydrateAuthorAsync(match, ct);
        }
        return null;
    }

    public async Task<PostTreatmentNote?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default)
    {
        List<string> jsons;
        if (_db.Database.IsNpgsql())
        {
            var needle = JsonSerializer.Serialize(new[] { new { RequestId = requestId } });
            jsons = await _db.Visits.AsNoTracking()
                .Where(v => v.PostTreatmentNotesJson != null && EF.Functions.JsonContains(v.PostTreatmentNotesJson, needle))
                .Select(v => v.PostTreatmentNotesJson!)
                .ToListAsync(ct);
        }
        else
        {
            jsons = await _db.Visits.AsNoTracking()
                .Where(v => v.PostTreatmentNotesJson != null)
                .Select(v => v.PostTreatmentNotesJson!)
                .ToListAsync(ct);
        }
        foreach (var json in jsons)
        {
            var match = ReadNotes(json).FirstOrDefault(n => n.RequestId == requestId);
            if (match is not null) return await HydrateAuthorAsync(match, ct);
        }
        return null;
    }

    public async Task<IReadOnlyList<PostTreatmentNote>> GetByVisitIdAsync(int visitId, CancellationToken ct = default)
    {
        var json = await _db.Visits.AsNoTracking()
            .Where(v => v.VisitId == visitId)
            .Select(v => v.PostTreatmentNotesJson)
            .FirstOrDefaultAsync(ct);
        var notes = ReadNotes(json);
        if (notes.Count == 0) return [];
        var authorIds = notes.Select(n => n.CreatedByUserId).Distinct().ToArray();
        var authors = await _db.Users.AsNoTracking().Include(u => u.Role)
            .Where(u => authorIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, ct);
        foreach (var note in notes)
            if (authors.TryGetValue(note.CreatedByUserId, out var author)) note.CreatedByUser = author;
        return notes.OrderBy(n => n.CreatedAt).ToList();
    }

    public async Task AddAsync(PostTreatmentNote note, CancellationToken ct = default)
    {
        var visit = _db.Visits.Local.FirstOrDefault(v => v.VisitId == note.VisitId)
            ?? await _db.Visits.FirstAsync(v => v.VisitId == note.VisitId, ct);
        if (note.Id <= 0)
        {
            var maxId = 0;
            var allJsons = await _db.Visits.AsNoTracking()
                .Where(v => v.PostTreatmentNotesJson != null)
                .Select(v => v.PostTreatmentNotesJson!)
                .ToListAsync(ct);
            foreach (var json in allJsons)
                foreach (var n in ReadNotes(json))
                    if (n.Id > maxId) maxId = n.Id;
            foreach (var tracked in _db.Visits.Local.Where(v => v.PostTreatmentNotesJson != null))
                foreach (var n in ReadNotes(tracked.PostTreatmentNotesJson))
                    if (n.Id > maxId) maxId = n.Id;
            note.Id = maxId + 1;
        }
        var notes = ReadNotes(visit.PostTreatmentNotesJson);
        notes.Add(note);
        visit.PostTreatmentNotesJson = JsonSerializer.Serialize(notes);
    }
}
