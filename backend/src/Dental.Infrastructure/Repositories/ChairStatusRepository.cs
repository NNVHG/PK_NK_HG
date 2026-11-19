using Dental.Application.Common;
using Dental.Application.Features.MOD_ASS.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class ChairStatusRepository(DentalDbContext db, VietnamClock? clock = null) : IChairStatusRepository
{
    private DateOnly Today => (clock ?? new VietnamClock(TimeProvider.System)).Today;

    public Task<bool> ActorAllowedAsync(int actorId, string role, CancellationToken ct = default)
        => db.Users.AnyAsync(x => x.UserId == actorId && x.IsActive && x.Role.RoleCode == role &&
            (role == RoleCodes.Admin || role == RoleCodes.Assistant), ct);

    public Task<DentalChair?> FindAsync(int id, CancellationToken ct = default)
        => db.DentalChairs.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> IsAssignedAsync(int id, CancellationToken ct = default)
    {
        var today = Today;
        return db.StaffWorkspaces.AnyAsync(workspace => workspace.WorkDate == today && workspace.User.IsActive &&
            workspace.User.Role.RoleCode == RoleCodes.Dentist &&
            (workspace.ChairId == id || (workspace.RoomId.HasValue &&
                db.DentalChairs.Any(chair => chair.Id == id && chair.RoomId == workspace.RoomId))), ct);
    }

    public Task<bool> HasActiveVisitAsync(int id, CancellationToken ct = default)
        => db.Visits.AnyAsync(visit => visit.Status == VisitStatuses.InProgress &&
            (visit.ChairId == id || (visit.RoomId.HasValue &&
                db.DentalChairs.Any(chair => chair.Id == id && chair.RoomId == visit.RoomId))), ct);

    public async Task<PagedResult<ChairStatusResponse>> GetPageAsync(ChairStatusQuery request, CancellationToken ct = default)
    {
        var today = Today;
        var query = db.DentalChairs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToLower();
            query = query.Where(x => x.ChairNumber.ToLower().Contains(keyword) || x.RoomName.ToLower().Contains(keyword));
        }
        if (request.Status.HasValue) query = query.Where(x => x.Status == (DentalChairStatus)request.Status.Value);
        if (request.IsActive.HasValue) query = query.Where(x => x.IsActive == request.IsActive.Value);
        var count = await query.CountAsync(ct);
        var dailyAssignments = db.StaffWorkspaces.AsNoTracking()
            .Where(w => w.WorkDate == today && w.User.IsActive);
        var dentists = dailyAssignments.Where(w => w.ChairId.HasValue && w.User.Role.RoleCode == RoleCodes.Dentist);
        var page = await query.OrderBy(x => x.RoomName).ThenBy(x => x.ChairNumber).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(chair => new
            {
                Chair = chair,
                Dentist = dentists.Where(w => w.ChairId == chair.Id || (chair.RoomId != null && w.RoomId == chair.RoomId))
                    .OrderByDescending(w => w.ChairId == chair.Id).ThenBy(w => w.UserId)
                    .Select(w => new { w.UserId, w.User.FullName }).FirstOrDefault(),
                HasActiveVisit = db.Visits.Any(v => v.Status == VisitStatuses.InProgress &&
                    (v.ChairId == chair.Id || (chair.RoomId != null && v.RoomId == chair.RoomId)))
            }).ToListAsync(ct);
        var dentistIds = page.Where(x => x.Dentist is not null).Select(x => x.Dentist!.UserId).Distinct().ToArray();
        var assistants = await dailyAssignments
            .Where(w => w.DentistId.HasValue && dentistIds.Contains(w.DentistId.Value) && w.User.Role.RoleCode == RoleCodes.Assistant)
            .OrderBy(w => w.User.FullName).ThenBy(w => w.UserId)
            .Select(w => new { w.DentistId, w.UserId, w.User.FullName })
            .ToListAsync(ct);
        var assistantsByDentist = assistants.ToLookup(w => w.DentistId!.Value);
        var items = page.Select(row => ChairStatusResponse.From(row.Chair) with
        {
            IsAssigned = row.Dentist is not null,
            HasActiveVisit = row.HasActiveVisit,
            DentistId = row.Dentist?.UserId,
            DentistName = row.Dentist?.FullName,
            Assistants = row.Dentist is null ? [] : assistantsByDentist[row.Dentist.UserId]
                .Select(assistant => new ChairStatusAssistant(assistant.UserId, assistant.FullName)).ToArray()
        }).ToArray();
        return PagedResult<ChairStatusResponse>.Create(items, count, request.Page, request.PageSize);
    }
}
