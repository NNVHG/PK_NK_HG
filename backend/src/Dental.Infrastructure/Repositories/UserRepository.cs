using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly DentalDbContext _db;

    public UserRepository(DentalDbContext db) => _db = db;

    public async Task<User?> FindByPhoneAsync(string phone, CancellationToken ct = default)
        => await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Phone == phone, ct);

    public async Task<User?> FindByIdAsync(int userId, CancellationToken ct = default)
        => await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);
}
