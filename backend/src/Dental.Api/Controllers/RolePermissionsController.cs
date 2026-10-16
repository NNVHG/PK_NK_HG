using Dental.Application.Features.Auth.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/admin/role-permissions")]
[Authorize(Policy = AppPolicies.AdminOnly)]
public sealed class RolePermissionsController(RolePermissionMatrixService service, ICurrentUser user) : ControllerBase
{
    [HttpGet]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (user.UserId is not int id || id <= 0) return Unauthorized();
        var result = await service.GetAsync(id, user.RoleCode, ct);
        return result.IsSuccess ? Ok(result.Value) : StatusCode(result.Error.Code switch
        {
            "AUTH_003" => 401, "GEN_002" => 403, _ => 503
        }, new { code = result.Error.Code, message = result.Error.Message });
    }
}
