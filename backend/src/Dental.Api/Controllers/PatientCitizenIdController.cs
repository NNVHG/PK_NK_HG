using Dental.Application.Features.MOD_PAT;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId:int}/citizen-id")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PatientCitizenIdController(PatientCitizenIdService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AppPolicies.PatientView)]
    public async Task<IActionResult> Read(int patientId, CancellationToken ct)
    {
        if (actor.UserId is not int id) return Unauthorized();
        var result = await service.ReadAsync(patientId, id, actor.RoleCode, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error.Code, result.Error.Message);
    }
    [HttpPut]
    [Authorize(Policy = AppPolicies.PatientManage)]
    public async Task<IActionResult> Save(int patientId, CitizenIdRequest request, CancellationToken ct)
    {
        if (actor.UserId is not int id) return Unauthorized();
        var result = await service.SaveAsync(patientId, id, actor.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error.Code, result.Error.Message);
    }
    private IActionResult Failure(string code, string message) => StatusCode(code switch {
        "GEN_001" => 404, "GEN_002" => 403, "PAT_ID_DUPLICATE" or "PAT_ID_CONFLICT" => 409, _ => 400 }, new { code, message });
}
