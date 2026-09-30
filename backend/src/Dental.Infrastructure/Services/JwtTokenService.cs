using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Dental.Infrastructure.Services;

/// <summary>
/// Tạo JWT access token HS256, sống 30 phút.
/// Secret key lấy từ user-secrets / biến môi trường (không nằm trong appsettings.json).
/// [DL-P01/DL-P02 tạm chốt: HS256, 30 phút — cần xác nhận]
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public string GenerateAccessToken(User user)
    {
        var secretKey = _config["Jwt:Secret"]
            ?? throw new InvalidOperationException(
                "Jwt:Secret chưa được cấu hình. Dùng 'dotnet user-secrets set Jwt:Secret <value>'.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(ClaimTypes.Role, user.Role.RoleCode),           // dùng RoleCode không dấu
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "DentalClinic",
            audience: _config["Jwt:Audience"] ?? "DentalClinicClient",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
