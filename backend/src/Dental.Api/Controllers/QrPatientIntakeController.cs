using Dental.Application.Common;
using Dental.Application.Features.MOD_PAT;
using Dental.Application.Features.Patients.Errors;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;
[ApiController]
[Route("api/patients")]
[Authorize(Policy = AppPolicies.PatientManage)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(8192)]
public sealed class QrPatientIntakeController(QrPatientIntakeService service, ICurrentUser actor) : ControllerBase
{
    [HttpPost("check-cccd")]
    public async Task<IActionResult> Lookup(QrIdentityLookupRequest request, CancellationToken ct)
    {
        if (actor.UserId is not int id) return Unauthorized();
        var result = await service.LookupAsync(id, actor.RoleCode, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }
    [HttpPost("qr-intake")]
    public async Task<IActionResult> Create(QrPatientIntakeRequest request, CancellationToken ct)
    {
        if (actor.UserId is not int id) return Unauthorized();
        var result = await service.CreateAsync(id, actor.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return result.IsSuccess ? StatusCode(201, result.Value) : Failure(result.Error);
    }
    private IActionResult Failure(Error error) => error is PatientPossibleDuplicateError duplicate
        ? Conflict(new { code = error.Code, message = error.Message, duplicates = duplicate.Duplicates })
        : StatusCode(error.Code switch { "GEN_002" => 403, "PAT_QR_EXISTS" or "PAT_QR_CONFLICT" => 409, _ => 400 }, new { code = error.Code, message = error.Message });
}
