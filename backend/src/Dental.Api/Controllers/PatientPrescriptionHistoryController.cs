using Dental.Application.Common;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController, Route("api/patients/{patientId:int}/prescriptions"), Authorize(Policy = AppPolicies.PatientView)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PatientPrescriptionHistoryController(PrescriptionService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int patientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if (actor.UserId is not int id) return Unauthorized();
        return Respond(await service.PatientHistoryAsync(patientId, id, actor.RoleCode, new(page, pageSize), ct));
    }
    [HttpGet("{prescriptionId:int}")]
    public async Task<IActionResult> Detail(int patientId, int prescriptionId, CancellationToken ct)
    {
        if (actor.UserId is not int id) return Unauthorized();
        return Respond(await service.PatientHistoryDetailAsync(patientId, prescriptionId, id, actor.RoleCode, ct));
    }
    private IActionResult Respond<T>(Result<T> result) => result.IsSuccess ? Ok(result.Value) : StatusCode(result.Error.Code switch
        { "GEN_001" => 404, "GEN_002" => 403, _ => 400 }, new { code = result.Error.Code, message = result.Error.Message });
}
