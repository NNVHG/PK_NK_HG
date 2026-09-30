using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

public interface ITokenService
{
    /// <summary>Tạo JWT access token cho user (HS256, 30 phút).</summary>
    string GenerateAccessToken(User user);
}
