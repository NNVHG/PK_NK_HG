using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController]
[Route("api/master/diagnoses")]
[Authorize]
public sealed class DentalDiagnosesController(DiagnosisCatalogService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AppPolicies.DiagnosisCatalogView)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get([FromQuery] DiagnosisQuery request, CancellationToken ct)
    { var result = await service.GetAsync(actor.RoleCode, request, ct); return result.IsSuccess ? Ok(result.Value) : Failure(result.Error); }
    [HttpPost]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public async Task<IActionResult> Create(DiagnosisWriteRequest request, CancellationToken ct)
    { if (actor.UserId is not int id) return Unauthorized(); var result = await service.SaveAsync(null, id, actor.RoleCode, request, ct); return result.IsSuccess ? StatusCode(201, result.Value) : Failure(result.Error); }
    [HttpPut("{id:int}")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public async Task<IActionResult> Update(int id, DiagnosisWriteRequest request, CancellationToken ct)
    { if (actor.UserId is not int userId) return Unauthorized(); var result = await service.SaveAsync(id, userId, actor.RoleCode, request, ct); return result.IsSuccess ? Ok(result.Value) : Failure(result.Error); }
    private IActionResult Failure(Dental.Application.Common.Error error) => StatusCode(error.Code switch { "GEN_001" => 404, "GEN_002" => 403, "MST_071" or "MST_072" => 409, _ => 400 }, new { code = error.Code, message = error.Message });
}
