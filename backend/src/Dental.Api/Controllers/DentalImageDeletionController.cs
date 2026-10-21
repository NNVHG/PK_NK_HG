using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/dental-images")]
[Authorize(Policy = AppPolicies.DentalImageDelete)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(8192)]
public sealed class DentalImageDeletionController(DentalImageDeletionService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet("deletion")]
    public async Task<IActionResult> List([FromQuery] int patientId, [FromQuery] int beforeId, CancellationToken ct)
    {
        if (actor.UserId is not int user) return Unauthorized();
        var r = await service.ListAsync(patientId, beforeId, user, actor.RoleCode, ct);
        return r.IsSuccess ? Ok(r.Value) : Failure(r.Error);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromBody] DeleteDentalImageRequest request, CancellationToken ct)
    {
        if (actor.UserId is not int user) return Unauthorized();
        var r = await service.DeleteAsync(id, user, actor.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return r.IsSuccess ? NoContent() : Failure(r.Error);
    }

    private IActionResult Failure(Error e) => StatusCode(e.Code switch
    {
        "GEN_001" => 404,
        "GEN_002" => 403,
        "IMG_DELETE_CONFLICT" or "IMG_DELETE_LOCKED" => 409,
        _ => 400
    }, new { code = e.Code, message = e.Message });
}
