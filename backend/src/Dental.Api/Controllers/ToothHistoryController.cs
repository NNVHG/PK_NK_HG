using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Features.MOD_FDI.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController]
[Route("api/patients/{patientId:int}")]
[Authorize(Policy = AppPolicies.PatientView)]
public sealed class ToothHistoryController(ToothHistoryService service, ICurrentUser user) : ControllerBase
{
    [HttpGet("teeth/{toothNumber:int}/history")]
    public Task<IActionResult> Tooth(int patientId, int toothNumber, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) => Get(new(patientId, toothNumber, page, pageSize), ct);
    [HttpGet("treatment-timeline")]
    public Task<IActionResult> Timeline(int patientId, [FromQuery] int? toothNumber = null, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) => Get(new(patientId, toothNumber, page, pageSize), ct);
    private async Task<IActionResult> Get(ToothHistoryRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (user.UserId is not int actorId) return Unauthorized();
        var result = await service.GetAsync(request, actorId, user.RoleCode, ct);
        return result.IsSuccess ? Ok(result.Value) : StatusCode(result.Error.Code switch
        { "GEN_001" => 404, "GEN_002" => 403, _ => 400 }, new { code = result.Error.Code, message = result.Error.Message });
    }
}
