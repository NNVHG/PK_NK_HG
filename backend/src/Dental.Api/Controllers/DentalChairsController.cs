using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/master/chairs")]
[Authorize]
public sealed class DentalChairsController(DentalChairService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AppPolicies.DentalChairView)]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetPage([FromQuery] DentalChairQuery request, CancellationToken ct)
    {
        var result = await service.GetPageAsync(actor.RoleCode, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
    [HttpPost]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public async Task<IActionResult> Create([FromBody] CreateDentalChairRequest request, CancellationToken ct)
    {
        if (actor.UserId is not int actorId) return Unauthorized();
        var result = await service.CreateAsync(actorId, actor.RoleCode, request, ct);
        return result.IsSuccess ? StatusCode(201, result.Value) : Failure(result.Error);
    }
    [HttpPut("{id:int}")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDentalChairRequest request, CancellationToken ct)
    {
        if (actor.UserId is not int actorId) return Unauthorized();
        var result = await service.UpdateAsync(id, actorId, actor.RoleCode, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
    private IActionResult Failure(Error error) => StatusCode(error.Code switch
    { "GEN_001" => 404, "GEN_002" => 403, "MST_051" or "MST_052" or "MST_053" or "MST_054" => 409, _ => 400 }, new { code = error.Code, message = error.Message });
}
