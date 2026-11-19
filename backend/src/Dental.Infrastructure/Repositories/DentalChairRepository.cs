using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class DentalChairRepository(DentalDbContext db, VietnamClock? clock = null) : IDentalChairRepository
{
    private DateOnly Today => (clock ?? new VietnamClock(TimeProvider.System)).Today;

    public async Task<IReadOnlyList<DentalChair>> GetAllAsync(CancellationToken ct = default)
        => await db.DentalChairs.AsNoTracking().OrderBy(x => x.RoomName).ThenBy(x => x.ChairNumber).ToListAsync(ct);

    public Task<bool> IsAssignedAsync(int chairId, CancellationToken ct = default)
    {
        var today = Today;
        return db.StaffWorkspaces.AnyAsync(workspace => workspace.WorkDate == today && workspace.User.IsActive &&
            workspace.User.Role.RoleCode == RoleCodes.Dentist &&
            (workspace.ChairId == chairId || (workspace.RoomId.HasValue &&
                db.DentalChairs.Any(chair => chair.Id == chairId && chair.RoomId == workspace.RoomId))), ct);
    }

    public Task<DentalChair?> FindAsync(int id, CancellationToken ct = default)
        => db.DentalChairs.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> HasActiveVisitAsync(int chairId, int? roomId, CancellationToken ct = default)
        => db.Visits.AnyAsync(visit => visit.Status == VisitStatuses.InProgress &&
            (visit.ChairId == chairId || (roomId.HasValue && visit.RoomId == roomId)), ct);

    public Task<bool> NumberExistsAsync(string normalizedNumber, CancellationToken ct = default)
        => db.DentalChairs.AnyAsync(x => x.ChairNumber == normalizedNumber, ct);

    public async Task AddAsync(DentalChair chair, CancellationToken ct = default)
        => await db.DentalChairs.AddAsync(chair, ct);

    public async Task<PagedResult<DentalChair>> GetPageAsync(DentalChairQuery request, CancellationToken ct = default)
    {
        var query = db.DentalChairs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToLower();
            query = query.Where(x => x.ChairNumber.ToLower().Contains(keyword) || x.RoomName.ToLower().Contains(keyword));
        }
        if (request.Status.HasValue) query = query.Where(x => x.Status == (DentalChairStatus)request.Status.Value);
        if (request.IsActive.HasValue) query = query.Where(x => x.IsActive == request.IsActive.Value);
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.RoomName).ThenBy(x => x.ChairNumber).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return PagedResult<DentalChair>.Create(items, count, request.Page, request.PageSize);
    }
}
