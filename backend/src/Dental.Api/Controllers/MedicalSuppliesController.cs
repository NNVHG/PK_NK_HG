using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/master/medical-supplies")]
[Authorize]
public sealed class MedicalSuppliesController(MedicalSupplyCatalogService service, ICurrentUser user) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApiPolicies.MedicalSupplyCatalogView)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get([FromQuery] MedicalSupplyQuery query, CancellationToken ct) =>
        Map(await service.GetAsync(user.RoleCode, query, ct));

    [HttpPost]
    [Authorize(Policy = ApiPolicies.AdminOnly)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Create([FromBody] MedicalSupplyWriteRequest request, CancellationToken ct) =>
        Map(await service.SaveAsync(null, user.UserId, user.RoleCode, request, ct), true);

    [HttpPut("{id:int}")]
    [Authorize(Policy = ApiPolicies.AdminOnly)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Update(int id, [FromBody] MedicalSupplyWriteRequest request, CancellationToken ct) =>
        Map(await service.SaveAsync(id, user.UserId, user.RoleCode, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = ApiPolicies.AdminOnly)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Deactivate(int id, [FromQuery] int expectedVersion, CancellationToken ct) =>
        Map(await service.DeactivateAsync(id, expectedVersion, user.UserId, user.RoleCode, ct));

    private IActionResult Map<T>(Result<T> r, bool created = false) =>
        r.IsSuccess ? (created ? StatusCode(201, r.Value) : Ok(r.Value))
        : r.Error.Code == Error.Unauthorized.Code ? Unauthorized(new { code = r.Error.Code, message = r.Error.Message })
        : r.Error.Code == Error.Forbidden.Code ? StatusCode(403, new { code = r.Error.Code, message = r.Error.Message })
        : r.Error.Code == Error.NotFound.Code ? NotFound(new { code = r.Error.Code, message = r.Error.Message })
        : r.Error.Code is "MST_091" or "MST_092" ? Conflict(new { code = r.Error.Code, message = r.Error.Message })
        : BadRequest(new { code = r.Error.Code, message = r.Error.Message });
}
