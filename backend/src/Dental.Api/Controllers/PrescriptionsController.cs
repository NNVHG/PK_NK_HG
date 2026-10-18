using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;
namespace Dental.Api.Controllers;
[ApiController]
[Route("api/visits/{visitId:int}/prescription")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PrescriptionsController(PrescriptionService service, ICurrentUser actor) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AppPolicies.PatientView)]
    public async Task<IActionResult> Get(int visitId, CancellationToken ct)
    { if (actor.UserId is not int id) return Unauthorized(); var r = await service.GetAsync(visitId, id, actor.RoleCode, ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error); }
    [HttpGet("history")]
    [Authorize(Policy = AppPolicies.PatientView)]
    public async Task<IActionResult> History(int visitId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    { if (actor.UserId is not int id) return Unauthorized(); var r = await service.HistoryAsync(visitId, id, actor.RoleCode, page, pageSize, ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error); }
    [HttpPost("{prescriptionId:int}/cancel")]
    [Authorize(Policy = AppPolicies.PrescriptionWrite)]
    public async Task<IActionResult> Cancel(int visitId, int prescriptionId, PrescriptionCancellationRequest request, CancellationToken ct)
    { if (actor.UserId is not int id) return Unauthorized(); var r = await service.CancelAsync(visitId, prescriptionId, id, actor.RoleCode, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error); }
    [HttpGet("lookup-link")]
    [Authorize(Policy = AppPolicies.PatientView)]
    public async Task<IActionResult> LookupLink(int visitId, CancellationToken ct)
    { if (actor.UserId is not int id) return Unauthorized(); var r = await service.LookupLinkAsync(visitId, id, actor.RoleCode, ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error); }
    [HttpGet("medicines")]
    [Authorize(Policy = AppPolicies.PrescriptionWrite)]
    public async Task<IActionResult> Medicines(int visitId, [FromQuery] MedicineQuery query, CancellationToken ct)
    { if (actor.UserId is not int id) return Unauthorized(); var r = await service.MedicinesAsync(visitId, id, actor.RoleCode, query, ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error); }
    [HttpGet("/api/prescriptions/patient/{patientId:int}/latest")]
    [Authorize(Policy = AppPolicies.PrescriptionWrite)]
    public async Task<IActionResult> Latest(int patientId, [FromQuery] int targetVisitId, CancellationToken ct)
    { if (actor.UserId is not int id) return Unauthorized(); var r = await service.LatestAsync(patientId, targetVisitId, id, actor.RoleCode, ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error); }
    [HttpPost("preview")]
    [Authorize(Policy = AppPolicies.PrescriptionWrite)]
    public async Task<IActionResult> Preview(int visitId, PrescriptionWriteRequest request, CancellationToken ct)
    { if (actor.UserId is not int id) return Unauthorized(); var r = await service.PreviewAsync(visitId, id, actor.RoleCode, request, ct); return r.IsSuccess ? Ok(r.Value) : Failure(r.Error); }
    [HttpPost]
    [Authorize(Policy = AppPolicies.PrescriptionWrite)]
    public Task<IActionResult> Create(int visitId, PrescriptionWriteRequest request, CancellationToken ct) => Save(visitId, request, true, ct);
    [HttpPut]
    [Authorize(Policy = AppPolicies.PrescriptionWrite)]
    public Task<IActionResult> Update(int visitId, PrescriptionWriteRequest request, CancellationToken ct) => Save(visitId, request, false, ct);
    private async Task<IActionResult> Save(int visitId, PrescriptionWriteRequest request, bool creating, CancellationToken ct)
    {
        if (actor.UserId is not int id) return Unauthorized();
        var r = await service.SaveAsync(visitId, id, actor.RoleCode, creating, request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return r.IsSuccess ? StatusCode(creating ? 201 : 200, r.Value) : Failure(r.Error);
    }
    private IActionResult Failure(Dental.Application.Common.Error error) => StatusCode(error.Code switch {
        "GEN_001" => 404, "GEN_002" => 403, "RX_002" or "RX_003" or "RX_004" or "RX_006" or "RX_007" or "RX_008" or "BIL_006" or "BIL_007" => 409, _ => 400 },
        new { code = error.Code, message = error.Message });
}
