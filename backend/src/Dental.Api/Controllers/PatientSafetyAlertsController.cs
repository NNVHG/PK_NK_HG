using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId:int}/safety-alerts")]
[Authorize(Policy = AppPolicies.PatientSafetyAlertsView)]
public sealed class PatientSafetyAlertsController : ControllerBase
{
    private readonly PatientSafetyAlertsService _service;

    public PatientSafetyAlertsController(PatientSafetyAlertsService service)
        => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(PatientSafetyAlertsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int patientId, CancellationToken ct)
    {
        var result = await _service.GetAsync(patientId, ct);
        return result.IsFailure
            ? StatusCode(StatusCodes.Status404NotFound, new { code = result.Error.Code, message = result.Error.Message })
            : Ok(result.Value);
    }
}
