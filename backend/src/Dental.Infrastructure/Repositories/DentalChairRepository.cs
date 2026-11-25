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

    public async Task<IReadOnlyList<ClinicRoom>> GetAllAsync(CancellationToken ct = default)
        => await db.ClinicRooms.AsNoTracking().OrderBy(x => x.RoomName).ThenBy(x => x.RoomCode).ToListAsync(ct);

    public Task<bool> IsAssignedAsync(int roomId, CancellationToken ct = default)
    {
        var today = Today;
        return db.Users.AnyAsync(u => u.WorkspaceDate == today && u.IsActive &&
            u.Role.RoleCode == RoleCodes.Dentist &&
            u.WorkspaceRoomId == roomId, ct);
    }

    public Task<ClinicRoom?> FindAsync(int id, CancellationToken ct = default)
        => db.ClinicRooms.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> HasActiveVisitAsync(int roomId, CancellationToken ct = default)
        => db.Visits.AnyAsync(visit => visit.Status == VisitStatuses.InProgress && visit.RoomId == roomId, ct);

    public Task<bool> NumberExistsAsync(string normalizedNumber, CancellationToken ct = default)
        => db.ClinicRooms.AnyAsync(x => x.RoomCode == normalizedNumber, ct);

    public Task<bool> RoomNameExistsAsync(string normalizedRoomName, int? excludeId = null, CancellationToken ct = default)
        => db.ClinicRooms.AnyAsync(x => x.NormalizedRoomName == normalizedRoomName && (!excludeId.HasValue || x.Id != excludeId.Value), ct);

    public async Task AddAsync(ClinicRoom room, CancellationToken ct = default)
        => await db.ClinicRooms.AddAsync(room, ct);

    public async Task<PagedResult<ClinicRoom>> GetPageAsync(DentalChairQuery request, CancellationToken ct = default)
    {
        var query = db.ClinicRooms.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToLower();
            query = query.Where(x => x.RoomCode.ToLower().Contains(keyword) || x.RoomName.ToLower().Contains(keyword));
        }
        if (request.Status.HasValue) query = query.Where(x => x.Status == (DentalChairStatus)request.Status.Value);
        if (request.IsActive.HasValue) query = query.Where(x => x.IsActive == request.IsActive.Value);
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.RoomName).ThenBy(x => x.RoomCode).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return PagedResult<ClinicRoom>.Create(items, count, request.Page, request.PageSize);
    }
}
