using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Features.MOD_FDI.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/visits/{visitId:int}/tooth-conditions")]
[Authorize]
public sealed class ToothConditionsController(ToothConditionService service, ICurrentUser user) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AppPolicies.PatientView)]
    public async Task<IActionResult> Get(int visitId, CancellationToken ct)
    {
        if (user.UserId is not int userId) return Unauthorized();
        var result = await service.GetAsync(visitId, userId, user.RoleCode, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.FdiConditionWrite)]
    public async Task<IActionResult> Add(int visitId, ToothConditionRequest request, CancellationToken ct)
    {
        if (user.UserId is not int userId) return Unauthorized();
        var result = await service.AddAsync(visitId, request, userId, user.RoleCode,
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsFailure ? Failure(result.Error) : StatusCode(201, result.Value);
    }

    private IActionResult Failure(Error error) => StatusCode(error.Code switch
    {
        "GEN_001" => 404, "GEN_002" => 403, "FDI_003" => 409, _ => 400
    }, new { code = error.Code, message = error.Message });
}
