using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController, Route("api/prescriptions/{id:int}"), Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PrescriptionHandoversController(PrescriptionHandoverService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet("dispense"), Authorize(Policy = AppPolicies.PatientView)]
    public async Task<IActionResult> Read(int id, CancellationToken ct) => actor.UserId is not int userId ? Unauthorized() : Respond(await service.ReadAsync(id, userId, actor.RoleCode, ct));
    [HttpPost("dispense"), Authorize(Policy = AppPolicies.PrescriptionHandover)]
    public async Task<IActionResult> Dispense(int id, PrescriptionDispenseRequest request, CancellationToken ct) => actor.UserId is not int userId ? Unauthorized() : Respond(await service.DispenseAsync(id, userId, actor.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));
    [HttpPost("undo-dispense"), Authorize(Policy = AppPolicies.PrescriptionHandover)]
    public async Task<IActionResult> Undo(int id, PrescriptionUndoDispenseRequest request, CancellationToken ct) => actor.UserId is not int userId ? Unauthorized() : Respond(await service.UndoAsync(id, userId, actor.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));
    private IActionResult Respond(Dental.Application.Common.Result<PrescriptionHandoverStatus> r) => r.IsSuccess ? Ok(r.Value) : StatusCode(r.Error.Code switch { "GEN_001" => 404, "GEN_002" => 403, "RX_HANDOVER_LOCKED" or "RX_HANDOVER_CONFLICT" or "RX_STOCK_INSUFFICIENT" => 409, _ => 400 }, new { code = r.Error.Code, message = r.Error.Message });
}
