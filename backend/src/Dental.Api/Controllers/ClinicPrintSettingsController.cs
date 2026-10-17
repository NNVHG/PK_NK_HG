using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController]
[Route("api/clinic/print-settings")]
[Authorize(Policy = AppPolicies.AdminOnly)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ClinicPrintSettingsController(ClinicPrintSettingsService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(CancellationToken ct)
    { if (actor.UserId is not int id) return Unauthorized(); var r = await service.ReadAsync(id, actor.RoleCode, ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error.Code); }
    [HttpPut]
    public async Task<IActionResult> Save(ClinicPrintSettingsRequest request, CancellationToken ct)
    { if (actor.UserId is not int id) return Unauthorized(); var r = await service.SaveAsync(id, actor.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error.Code); }
    private IActionResult Failure(string code) => StatusCode(code switch { "GEN_002" => 403, "GEN_004" => 409, _ => 400 }, new { code, message = "Không lưu được cấu hình; kiểm tra quyền, dữ liệu và tải lại trước khi thử tiếp." });
}
