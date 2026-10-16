using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/workspaces")]
[Authorize(Policy = AppPolicies.WorkspaceManage)]
public sealed class StaffWorkspacesController(StaffWorkspaceService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet("me")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public Task<IActionResult> GetMe(CancellationToken ct) => Get(actor.UserId ?? 0, ct);
    [HttpGet("{id:int}")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var result = await service.GetAsync(actor.UserId ?? 0, actor.RoleCode, id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
    [HttpGet("options")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetOptions(CancellationToken ct)
    {
        var result = await service.GetOptionsAsync(actor.UserId ?? 0, actor.RoleCode, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
    [HttpGet("staff")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetTargets([FromQuery] WorkspaceTargetQuery request, CancellationToken ct)
    {
        var result = await service.GetTargetsAsync(actor.UserId ?? 0, actor.RoleCode, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Change(int id, [FromBody] ChangeWorkspaceRequest request, CancellationToken ct)
    {
        var result = await service.ChangeAsync(actor.UserId ?? 0, actor.RoleCode, id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
    private IActionResult Failure(Error error) => StatusCode(error.Code switch
    { "GEN_001" => 404, "GEN_002" => 403, "MST_061" or "MST_062" or "MST_063" or "MST_064" => 409, _ => 400 }, new { code = error.Code, message = error.Message });
}
