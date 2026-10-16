using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController]
[Route("api/medicines")]
[Authorize(Policy = AppPolicies.AdminOnly)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MedicinesController(MedicineCatalogService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] MedicineQuery request, CancellationToken ct)
    { var result = await service.GetAsync(actor.RoleCode, request, ct); return result.IsSuccess ? Ok(result.Value) : Failure(result.Error); }
    [HttpPost]
    public async Task<IActionResult> Create(MedicineWriteRequest request, CancellationToken ct)
    { if (actor.UserId is not int id) return Unauthorized(); var result = await service.SaveAsync(null, id, actor.RoleCode, request, ct); return result.IsSuccess ? StatusCode(201, result.Value) : Failure(result.Error); }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MedicineWriteRequest request, CancellationToken ct)
    { if (actor.UserId is not int userId) return Unauthorized(); var result = await service.SaveAsync(id, userId, actor.RoleCode, request, ct); return result.IsSuccess ? Ok(result.Value) : Failure(result.Error); }
    // DELETE means soft deactivation, preserving identity and future prescription references (DL-211).
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, [FromQuery] int expectedVersion, CancellationToken ct)
    { if (actor.UserId is not int userId) return Unauthorized(); var result = await service.DeactivateAsync(id, expectedVersion, userId, actor.RoleCode, ct); return result.IsSuccess ? Ok(result.Value) : Failure(result.Error); }
    private IActionResult Failure(Dental.Application.Common.Error error) => StatusCode(error.Code switch { "GEN_001" => 404, "GEN_002" => 403, "MST_081" or "MST_082" => 409, _ => 400 }, new { code = error.Code, message = error.Message });
}
