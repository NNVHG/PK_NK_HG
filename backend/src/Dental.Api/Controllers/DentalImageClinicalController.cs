using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;

[ApiController]
[Route("api/dental-images/{id:int}/clinical-notes")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(8192)]
public sealed class DentalImageClinicalController(DentalImageClinicalService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AppPolicies.DentalImageRead)]
    public async Task<IActionResult> Read(int id, CancellationToken ct)
    { if (actor.UserId is not int user) return Unauthorized(); var r = await service.ReadAsync(id, user, actor.RoleCode, ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error); }
    [HttpGet("addenda")]
    [Authorize(Policy = AppPolicies.DentalImageRead)]
    public async Task<IActionResult> History(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    { if (actor.UserId is not int user) return Unauthorized(); var r = await service.HistoryAsync(id, user, actor.RoleCode, page, pageSize, ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error); }
    [HttpPut]
    [Authorize(Policy = AppPolicies.DentalImageManage)]
    public async Task<IActionResult> Replace(int id, ImageClinicalWriteRequest request, CancellationToken ct)
    { if (actor.UserId is not int user) return Unauthorized(); var r = await service.ReplaceAsync(id, user, actor.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error); }
    [HttpPost("addenda")]
    [Authorize(Policy = AppPolicies.DentalImageManage)]
    public async Task<IActionResult> Append(int id, ImageClinicalAppendRequest request, CancellationToken ct)
    { if (actor.UserId is not int user) return Unauthorized(); var r = await service.AppendAsync(id, user, actor.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error); }
    private IActionResult Failure(Error error) => StatusCode(error.Code switch { "GEN_001" => 404, "GEN_002" => 403, "IMG_CLINICAL_CONFLICT" or "IMG_CLINICAL_LOCKED" => 409, _ => 400 }, new { code = error.Code, message = error.Message });
}
