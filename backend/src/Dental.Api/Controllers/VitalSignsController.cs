using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.VitalSigns.DTOs;
using Dental.Application.Features.VitalSigns.Services;
using Dental.Application.Features.VitalSigns.Validators;
using Dental.Application.Interfaces;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class VitalSignsController : ControllerBase
{
    private readonly VitalSignService _vitalSignService;
    private readonly ICurrentUser _currentUser;
    private readonly RecordVitalSignsRequestValidator _recordValidator;
    private readonly VitalSignQueryRequestValidator _queryValidator;

    public VitalSignsController(
        VitalSignService vitalSignService,
        ICurrentUser currentUser,
        RecordVitalSignsRequestValidator recordValidator,
        VitalSignQueryRequestValidator queryValidator)
    {
        _vitalSignService = vitalSignService;
        _currentUser = currentUser;
        _recordValidator = recordValidator;
        _queryValidator = queryValidator;
    }

    [HttpPost("visits/{visitId:int}/vital-signs")]
    [Authorize(Policy = AppPolicies.VitalSignsWrite)]
    [ProducesResponseType(typeof(VitalSignRecordResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Record(
        int visitId,
        [FromBody] RecordVitalSignsRequest request,
        CancellationToken ct)
    {
        if (_currentUser.UserId is not int recordedByUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var validation = await _recordValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        var result = await _vitalSignService.RecordAsync(
            visitId,
            recordedByUserId,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        if (result.IsFailure)
            return Failure(result.Error);

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("patients/{patientId:int}/vital-signs")]
    [Authorize(Policy = AppPolicies.PatientView)]
    [ProducesResponseType(typeof(PagedResult<VitalSignRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientVitalSigns(
        int patientId,
        [FromQuery] VitalSignQueryRequest request,
        CancellationToken ct)
    {
        if (_currentUser.UserId is not int currentUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var validation = await _queryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        var result = await _vitalSignService.GetPatientRecordsAsync(
            patientId, currentUserId, _currentUser.RoleCode, request, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    private IActionResult ToValidationProblem(ValidationResult validation)
        => BadRequest(new
        {
            code = Error.Validation.Code,
            message = "Dữ liệu đầu vào không hợp lệ.",
            errors = validation.Errors.Select(error => new { field = error.PropertyName, message = error.ErrorMessage }),
        });

    private IActionResult Failure(Error error)
    {
        var statusCode = error.Code switch
        {
            "GEN_001" => StatusCodes.Status404NotFound,
            "PAT_052" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return StatusCode(statusCode, new { code = error.Code, message = error.Message });
    }
}
