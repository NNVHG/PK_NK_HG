using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByPhoneAsync(string phone, CancellationToken ct = default);
    Task<User?> FindByIdAsync(int userId, CancellationToken ct = default);
}
