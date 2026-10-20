using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/dental-images/{id:int}/metadata")]
[Authorize(Policy = AppPolicies.DentalImageManage)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(8192)]
public sealed class DentalImageMetadataController(DentalImageMetadataService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(int id, CancellationToken ct)
    {
        if (actor.UserId is not int user) return Unauthorized();
        var result = await service.ReadAsync(id, user, actor.RoleCode, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
    [HttpPut]
    public async Task<IActionResult> Update(int id, UpdateDentalImageMetadataRequest request, CancellationToken ct)
    {
        if (actor.UserId is not int user) return Unauthorized();
        var result = await service.UpdateAsync(id, user, actor.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
    private IActionResult Failure(Error e) => StatusCode(e.Code switch
    {
        "GEN_001" => 404,
        "GEN_002" => 403,
        "IMG_METADATA_CONFLICT" or "IMG_METADATA_LOCKED" or "IMG_METADATA_LIMIT" => 409,
        _ => 400
    }, new { code = e.Code, message = e.Message });
}
