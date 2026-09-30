using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByPhoneAsync(string phone, CancellationToken ct = default);
    Task<User?> FindByIdAsync(int userId, CancellationToken ct = default);
    Task<Role?> FindRoleByCodeAsync(string roleCode, CancellationToken ct = default);
    Task<bool> PhoneExistsAsync(string phone, int? exceptUserId = null, CancellationToken ct = default);
    Task<int> CountActiveAdminsAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetStaffPageAsync(
        string? roleCode,
        bool? isActive,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}
