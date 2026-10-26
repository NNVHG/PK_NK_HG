using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly DentalDbContext _db;

    public UserRepository(DentalDbContext db) => _db = db;

    public async Task<IReadOnlyList<User>> GetActiveDentistsAsync(CancellationToken ct = default)
        => await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Role.RoleCode == RoleCodes.Dentist)
            .OrderBy(u => u.FullName).ThenBy(u => u.UserId)
            .ToListAsync(ct);

    public async Task<User?> FindByPhoneAsync(string phone, CancellationToken ct = default)
        => await _db.Users
            .Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.Phone == phone && !u.LoginByCitizenId, ct);

    public async Task<User?> FindByCitizenIdHashAsync(string citizenIdHash, CancellationToken ct = default)
    {
        var matches = await _db.Users.Include(u => u.Role).Where(u => u.LoginByCitizenId &&
            u.Role.RoleCode == RoleCodes.Patient && _db.Patients.Any(p => p.UserId == u.UserId && p.CitizenIdHash == citizenIdHash && p.IsActive))
            .Take(2).ToListAsync(ct);
        return matches.Count == 1 ? matches[0] : null;
    }

    public async Task<User?> FindByIdentityHashAsync(string identityHash, CancellationToken ct = default)
    {
        return await FindByCitizenIdHashAsync(identityHash, ct);
    }


    public async Task<User?> FindByIdAsync(int userId, CancellationToken ct = default)
        => await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

    public async Task<Role?> FindRoleByCodeAsync(string roleCode, CancellationToken ct = default)
        => await _db.Roles.FirstOrDefaultAsync(r => r.RoleCode == roleCode, ct);

    public Task<bool> PhoneExistsAsync(string phone, int? exceptUserId = null, CancellationToken ct = default)
        => _db.Users.AnyAsync(u => !u.LoginByCitizenId && u.Phone == phone && (!exceptUserId.HasValue || u.UserId != exceptUserId.Value), ct);

    public Task<int> CountActiveAdminsAsync(CancellationToken ct = default)
        // A pending first-login account cannot replace the last administrator who can actually sign in.
        => _db.Users.CountAsync(u => u.IsActive && !u.RequiresPhoneActivation && u.Role.RoleCode == RoleCodes.Admin, ct);

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetStaffPageAsync(
        string? roleCode,
        bool? isActive,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Where(u => RoleCodes.StaffRoles.Contains(u.Role.RoleCode));

        if (!string.IsNullOrWhiteSpace(roleCode))
            query = query.Where(u => u.Role.RoleCode == roleCode);

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(u => EF.Functions.ILike(u.FullName, pattern) || EF.Functions.ILike(u.Phone, pattern));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _db.Users.AddAsync(user, ct);
}
