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

    public Task<ClinicRoom?> FindAsync(int id, CancellationToken ct = default)
        => db.ClinicRooms.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> IsAssignedAsync(int id, CancellationToken ct = default)
    {
        var today = Today;
        return db.Users.AnyAsync(u => u.WorkspaceDate == today && u.IsActive &&
            u.Role.RoleCode == RoleCodes.Dentist && u.WorkspaceRoomId == id, ct);
    }

    public Task<bool> HasActiveVisitAsync(int id, CancellationToken ct = default)
        => db.Visits.AnyAsync(visit => visit.Status == VisitStatuses.InProgress && visit.RoomId == id, ct);

    public async Task<PagedResult<ChairStatusResponse>> GetPageAsync(ChairStatusQuery request, CancellationToken ct = default)
    {
        var today = Today;
        var query = db.ClinicRooms.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToLower();
            query = query.Where(x => x.RoomCode.ToLower().Contains(keyword) || x.RoomName.ToLower().Contains(keyword));
        }
        if (request.Status.HasValue) query = query.Where(x => x.Status == (DentalChairStatus)request.Status.Value);
        if (request.IsActive.HasValue) query = query.Where(x => x.IsActive == request.IsActive.Value);
        var count = await query.CountAsync(ct);
        var dailyAssignments = db.Users.AsNoTracking()
            .Where(u => u.WorkspaceDate == today && u.IsActive);
        var dentists = dailyAssignments.Where(u => u.WorkspaceRoomId.HasValue && u.Role.RoleCode == RoleCodes.Dentist);
        var page = await query.OrderBy(x => x.RoomName).ThenBy(x => x.RoomCode).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(room => new
            {
                Room = room,
                Dentist = dentists.Where(u => u.WorkspaceRoomId == room.Id)
                    .OrderBy(u => u.UserId)
                    .Select(u => new { u.UserId, u.FullName }).FirstOrDefault(),
                HasActiveVisit = db.Visits.Any(v => v.Status == VisitStatuses.InProgress && v.RoomId == room.Id)
            }).ToListAsync(ct);
        var dentistIds = page.Where(x => x.Dentist is not null).Select(x => x.Dentist!.UserId).Distinct().ToArray();
        var assistants = await dailyAssignments
            .Where(u => u.WorkspaceDentistId.HasValue && dentistIds.Contains(u.WorkspaceDentistId.Value) && u.Role.RoleCode == RoleCodes.Assistant)
            .OrderBy(u => u.FullName).ThenBy(u => u.UserId)
            .Select(u => new { DentistId = u.WorkspaceDentistId, u.UserId, u.FullName })
            .ToListAsync(ct);
        var assistantsByDentist = assistants.ToLookup(w => w.DentistId!.Value);
        var items = page.Select(row => ChairStatusResponse.From(row.Room) with
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
