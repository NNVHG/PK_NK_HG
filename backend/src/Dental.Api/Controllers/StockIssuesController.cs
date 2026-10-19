using Dental.Application.Common;
using Dental.Application.Features.MOD_INV;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;

[ApiController, Route("api/inventory/issues"), Authorize(Policy = ApiPolicies.StockIssueWrite)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StockIssuesController(StockIssueService service, ICurrentUser user) : ControllerBase
{
    [HttpGet("targets")]
    public async Task<IActionResult> Targets([FromQuery] int? visitId, [FromQuery] int beforeId = 0, CancellationToken ct = default) => Map(await service.TargetsAsync(visitId, beforeId, user.UserId, user.RoleCode, ct));
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int visitId, [FromQuery] int beforeId = 0, CancellationToken ct = default) => Map(await service.ListAsync(visitId, beforeId, user.UserId, user.RoleCode, ct));
    [HttpPost, RequestSizeLimit(65536)]
    public async Task<IActionResult> Confirm([FromBody] StockIssueRequest request, CancellationToken ct)
    {
        var r = await service.ConfirmAsync(request, user.UserId, user.RoleCode, ct);
        return r.IsSuccess ? StatusCode(r.Value!.Replayed ? 200 : 201, r.Value) : Map(r);
    }
    private IActionResult Map<T>(Result<T> r) => r.IsSuccess ? Ok(r.Value)
        : r.Error.Code == Error.Unauthorized.Code ? Unauthorized(new { code = r.Error.Code, message = r.Error.Message })
        : r.Error.Code == Error.Forbidden.Code ? StatusCode(403, new { code = r.Error.Code, message = r.Error.Message })
        : r.Error.Code == Error.NotFound.Code ? NotFound(new { code = r.Error.Code, message = r.Error.Message })
        : r.Error.Code is "INV_409" or "INV_STALE" or "INV_VISIT" or "INV_INSUFFICIENT" ? Conflict(new { code = r.Error.Code, message = r.Error.Message })
        : BadRequest(new { code = r.Error.Code, message = r.Error.Message });
}
