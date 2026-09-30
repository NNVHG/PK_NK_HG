using AppPolicies = Dental.Api.Policies.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

/// <summary>
/// Controller kiểm tra phân quyền — CHỈ TỒN TẠI TRONG DEVELOPMENT.
/// XÓA sau Sprint 1.
/// </summary>
[ApiController]
[Route("api/dev")]
[Authorize]
public sealed class DevController : ControllerBase
{
    /// <summary>Chỉ Admin mới gọi được (kỳ vọng 200 cho Admin, 403 cho vai trò khác).</summary>
    [HttpGet("admin-only")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public IActionResult AdminOnly()
        => Ok(new { message = "Bạn là Admin. Endpoint này chỉ dành cho ADMIN." });

    /// <summary>Mọi nhân viên nội bộ gọi được (trừ Bệnh nhân).</summary>
    [HttpGet("staff-only")]
    [Authorize(Policy = AppPolicies.StaffAny)]
    public IActionResult StaffOnly()
        => Ok(new { message = "Bạn là nhân viên. Endpoint này dành cho Staff." });
}
