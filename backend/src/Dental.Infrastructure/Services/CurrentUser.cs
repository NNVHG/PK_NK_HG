using System.Security.Claims;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Dental.Infrastructure.Services;

/// <summary>Đọc thông tin người dùng hiện tại từ JWT claim trong HttpContext.</summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal
        => _httpContextAccessor.HttpContext?.User;

    public int? UserId
    {
        get
        {
            var sub = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? Principal?.FindFirstValue("sub");
            return int.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? RoleCode => Principal?.FindFirstValue(ClaimTypes.Role);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
}
