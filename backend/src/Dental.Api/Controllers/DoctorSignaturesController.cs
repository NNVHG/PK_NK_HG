using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController]
[Route("api/doctors/{doctorId:int}/signature")]
[Authorize(Policy = AppPolicies.DoctorSignatureManage)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DoctorSignaturesController(DoctorSignatureService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(int doctorId, CancellationToken ct)
    {
        if (actor.UserId is not int id) return Unauthorized();
        var r = await service.ReadAsync(doctorId, id, actor.RoleCode, ct);
        return r.IsSuccess ? Ok(r.Value) : Failure(r.Error.Code, r.Error.Message);
    }
    [HttpGet("file")]
    public async Task<IActionResult> Image(int doctorId, [FromQuery] int version, CancellationToken ct)
    {
        if (actor.UserId is not int id) return Unauthorized();
        var r = await service.FileAsync(doctorId, version, id, actor.RoleCode, ct);
        if (r.IsFailure) return Failure(r.Error.Code, r.Error.Message);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(r.Value!, "image/png");
    }
    [HttpPost]
    [RequestSizeLimit(400000)]
    public async Task<IActionResult> Save(int doctorId, DoctorSignatureRequest request, CancellationToken ct)
    {
        if (actor.UserId is not int id) return Unauthorized();
        var r = await service.SaveAsync(doctorId, id, actor.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return r.IsSuccess ? Ok(r.Value) : Failure(r.Error.Code, r.Error.Message);
    }
    private IActionResult Failure(string code, string message) => StatusCode(code switch { "GEN_001" => 404, "GEN_002" => 403, "GEN_004" => 409, _ => 400 }, new { code, message });
}
