using Dental.Application.Common;
using Dental.Application.Features.MOD_INV;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;

[ApiController, Route("api/inventory"), Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StockReceiptsController(StockReceiptService service, ICurrentUser user) : ControllerBase
{
    [HttpGet("stock"), Authorize(Policy = ApiPolicies.StockView)]
    public async Task<IActionResult> Stock([FromQuery] string? keyword, [FromQuery] int page = 1, CancellationToken ct = default) => Map(await service.StockAsync(keyword, page, user.UserId, user.RoleCode, ct));
    [HttpGet("receipts"), Authorize(Policy = ApiPolicies.StockReceiptWrite)]
    public async Task<IActionResult> List([FromQuery] int beforeId = 0, CancellationToken ct = default) => Map(await service.ListAsync(beforeId, user.UserId, user.RoleCode, ct));
    [HttpGet("receipts/{id:int}"), Authorize(Policy = ApiPolicies.StockReceiptWrite)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Map(await service.GetAsync(id, user.UserId, user.RoleCode, ct));
    [HttpPost("receipts"), Authorize(Policy = ApiPolicies.StockReceiptWrite), RequestSizeLimit(65536)]
    public async Task<IActionResult> Confirm([FromBody] StockReceiptRequest request, CancellationToken ct)
    {
        var r = await service.ConfirmAsync(request, user.UserId, user.RoleCode, ct);
        return r.IsSuccess ? StatusCode(r.Value!.Replayed ? 200 : 201, r.Value) : Map(r);
    }
    private IActionResult Map<T>(Result<T> r) => r.IsSuccess ? Ok(r.Value)
        : r.Error.Code == Error.Unauthorized.Code ? Unauthorized(new { code = r.Error.Code, message = r.Error.Message })
        : r.Error.Code == Error.Forbidden.Code ? StatusCode(403, new { code = r.Error.Code, message = r.Error.Message })
        : r.Error.Code == Error.NotFound.Code ? NotFound(new { code = r.Error.Code, message = r.Error.Message })
        : r.Error.Code is "INV_409" or "INV_STALE" ? Conflict(new { code = r.Error.Code, message = r.Error.Message })
        : BadRequest(new { code = r.Error.Code, message = r.Error.Message });
}
