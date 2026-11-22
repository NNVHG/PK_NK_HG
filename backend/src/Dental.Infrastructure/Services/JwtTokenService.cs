using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Dental.Infrastructure.Services;

/// <summary>
/// Tạo JWT access token HS256 theo vai trò.
/// Secret key lấy từ user-secrets / biến môi trường (không nằm trong appsettings.json).
/// Mặc định giữ 30 phút nếu không cấu hình Jwt:StaffAccessTokenMinutes / Jwt:PatientAccessTokenMinutes.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private const int DefaultAccessTokenMinutes = 30;
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
            new Claim("token_version", user.TokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };

        var lifetimeMinutes = ResolveLifetimeMinutes(user);
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "DentalClinic",
            audience: _config["Jwt:Audience"] ?? "DentalClinicClient",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(lifetimeMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private int ResolveLifetimeMinutes(User user)
    {
        var configKey = user.Role?.RoleCode == RoleCodes.Patient
            ? "Jwt:PatientAccessTokenMinutes"
            : "Jwt:StaffAccessTokenMinutes";

        return int.TryParse(_config[configKey], out var minutes) && minutes > 0
            ? minutes
            : DefaultAccessTokenMinutes;
    }
}
