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
        return db.StaffWorkspaces.AnyAsync(x => x.ChairId == id && x.WorkDate == today, ct);
    }

    public Task<bool> HasActiveVisitAsync(int id, CancellationToken ct = default)
        => db.Visits.AnyAsync(x => x.ChairId == id && x.Status == VisitStatuses.InProgress, ct);

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
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.RoomName).ThenBy(x => x.ChairNumber).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new ChairStatusResponse(x.Id, x.ChairNumber, x.RoomName, (int)x.Status,
                ChairStatusResponse.Label(x.Status), x.IsActive, x.Version,
                db.StaffWorkspaces.Any(w => w.ChairId == x.Id && w.WorkDate == today),
                db.Visits.Any(v => v.ChairId == x.Id && v.Status == VisitStatuses.InProgress))).ToListAsync(ct);
        return PagedResult<ChairStatusResponse>.Create(items, count, request.Page, request.PageSize);
    }
}
