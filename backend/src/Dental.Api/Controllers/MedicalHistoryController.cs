using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.MedicalHistory.DTOs;
using Dental.Application.Features.MedicalHistory.Services;
using Dental.Application.Features.MedicalHistory.Validators;
using Dental.Application.Interfaces;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class MedicalHistoryController : ControllerBase
{
    private readonly MedicalHistoryService _medicalHistoryService;
    private readonly ICurrentUser _currentUser;
    private readonly RecordMedicalHistoryRequestValidator _recordValidator;
    private readonly MedicalHistoryQueryRequestValidator _queryValidator;

    public MedicalHistoryController(
        MedicalHistoryService medicalHistoryService,
        ICurrentUser currentUser,
        RecordMedicalHistoryRequestValidator recordValidator,
        MedicalHistoryQueryRequestValidator queryValidator)
    {
        _medicalHistoryService = medicalHistoryService;
        _currentUser = currentUser;
        _recordValidator = recordValidator;
        _queryValidator = queryValidator;
    }

    [HttpPost("visits/{visitId:int}/medical-history")]
    [Authorize(Policy = AppPolicies.MedicalHistoryWrite)]
    [ProducesResponseType(typeof(MedicalHistoryRecordResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Record(
        int visitId,
        [FromBody] RecordMedicalHistoryRequest request,
        CancellationToken ct)
    {
        if (_currentUser.UserId is not int recordedByUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var validation = await _recordValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        var result = await _medicalHistoryService.RecordAsync(
            visitId,
            recordedByUserId,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        if (result.IsFailure)
            return Failure(result.Error);

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("patients/{patientId:int}/medical-history/latest")]
    [Authorize(Policy = AppPolicies.PatientView)]
    [ProducesResponseType(typeof(MedicalHistoryRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatest(int patientId, CancellationToken ct)
    {
        if (_currentUser.UserId is not int currentUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _medicalHistoryService.GetLatestAsync(
            patientId, currentUserId, _currentUser.RoleCode, ct);
        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpGet("patients/{patientId:int}/medical-history")]
    [Authorize(Policy = AppPolicies.PatientView)]
    [ProducesResponseType(typeof(PagedResult<MedicalHistoryRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(
        int patientId,
        [FromQuery] MedicalHistoryQueryRequest request,
        CancellationToken ct)
    {
        if (_currentUser.UserId is not int currentUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var validation = await _queryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        var result = await _medicalHistoryService.GetHistoryAsync(
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
            "PAT_041" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return StatusCode(statusCode, new { code = error.Code, message = error.Message });
    }
}
