using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;

[ApiController, Route("api/master/configs"), Authorize(Policy = ApiPolicies.AdminOnly)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SchedulingSettingsController(SchedulingSettingsService service, ICurrentUser user) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(CancellationToken ct) => Map(await service.ReadAsync(user.UserId, user.RoleCode, ct));
    [HttpPut, RequestSizeLimit(8192)]
    public async Task<IActionResult> Save(SchedulingSettingsRequest request, CancellationToken ct) => Map(await service.SaveAsync(user.UserId, user.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));
    private IActionResult Map<T>(Dental.Application.Common.Result<T> r) => r.IsSuccess ? Ok(r.Value) : StatusCode(r.Error.Code switch { "GEN_002" => 403, "AUTH_003" => 401, "GEN_004" => 409, _ => 400 }, new { code = r.Error.Code, message = r.Error.Code == "GEN_004" ? "Cấu hình đã thay đổi. Tải lại trước khi lưu." : r.Error.Message });
}
