using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Features.MOD_FDI.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController]
[Route("api/visits/{visitId:int}/services/{assignmentId:int}")]
[Authorize(Policy = AppPolicies.FdiServiceAssign)]
public sealed class VisitServiceDeletionController(FdiServiceDeletionService service, ICurrentUser user) : ControllerBase
{
    [HttpDelete]
    public async Task<IActionResult> Delete(int visitId, int assignmentId, DeleteVisitServiceBody body, CancellationToken ct)
    {
        if (user.UserId is not int actorId) return Unauthorized();
        var result = await service.DeleteAsync(new(visitId, assignmentId, body.Reason), actorId, user.RoleCode,
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsSuccess ? NoContent() : StatusCode(result.Error.Code switch
        { "GEN_001" => 404, "GEN_002" => 403, "FDI_043" or "FDI_044" or "FDI_045" => 409, _ => 400 },
            new { code = result.Error.Code, message = result.Error.Message });
    }
}
