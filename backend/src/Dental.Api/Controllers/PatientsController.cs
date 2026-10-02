using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Errors;
using Dental.Application.Features.Patients.Services;
using Dental.Application.Features.Patients.Validators;
using Dental.Application.Interfaces;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/patients")]
public sealed class PatientsController : ControllerBase
{
    private readonly PatientsService _patientsService;
    private readonly ICurrentUser _currentUser;
    private readonly CreatePatientRequestValidator _createValidator;
    private readonly PatientDuplicateCheckRequestValidator _duplicateCheckValidator;
    private readonly PatientQueryRequestValidator _queryValidator;
    private readonly UpdatePatientRequestValidator _updateValidator;

    public PatientsController(
        PatientsService patientsService,
        ICurrentUser currentUser,
        CreatePatientRequestValidator createValidator,
        PatientDuplicateCheckRequestValidator duplicateCheckValidator,
        PatientQueryRequestValidator queryValidator,
        UpdatePatientRequestValidator updateValidator)
    {
        _patientsService = patientsService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _duplicateCheckValidator = duplicateCheckValidator;
        _queryValidator = queryValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.PatientView)]
    [ProducesResponseType(typeof(PagedResult<PatientListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPatients([FromQuery] PatientQueryRequest request, CancellationToken ct)
    {
        if (_currentUser.RoleCode == Dental.Domain.Constants.RoleCodes.Patient)
            return Forbid();

        var validation = await _queryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        var result = await _patientsService.GetPatientsAsync(request, ct);
        return Ok(result.Value);
    }

    [HttpPost("check-duplicate")]
    [Authorize(Policy = AppPolicies.PatientManage)]
    [ProducesResponseType(typeof(IReadOnlyList<PatientDuplicateInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckDuplicate(
        [FromBody] PatientDuplicateCheckRequest request,
        CancellationToken ct)
    {
        var validation = await _duplicateCheckValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        var result = await _patientsService.CheckDuplicatesAsync(request, ct);
        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.PatientManage)]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePatient([FromBody] CreatePatientRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (_currentUser.UserId is not int creatorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _patientsService.CreatePatientAsync(
            creatorUserId,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        if (result.IsFailure)
            return Failure(result.Error);

        return CreatedAtAction(nameof(GetPatient), new { id = result.Value!.PatientId }, result.Value);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = AppPolicies.PatientView)]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatient(int id, CancellationToken ct)
    {
        if (_currentUser.UserId is not int currentUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _patientsService.GetPatientByIdAsync(
            id,
            currentUserId,
            _currentUser.RoleCode,
            ct);

        return result.IsFailure ? Failure(result.Error) : Ok(result.Value);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AppPolicies.PatientEdit)]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdatePatient(int id, [FromBody] UpdatePatientRequest request, CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (_currentUser.UserId is not int actorUserId)
            return Unauthorized(new { code = Error.Unauthorized.Code, message = Error.Unauthorized.Message });

        var result = await _patientsService.UpdatePatientAsync(
            actorUserId,
            id,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

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
        if (error is PatientPossibleDuplicateError duplicateError)
            return Conflict(new
            {
                code = error.Code,
                message = error.Message,
                duplicates = duplicateError.Duplicates,
            });

        var statusCode = error.Code == Error.NotFound.Code
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        return StatusCode(statusCode, new { code = error.Code, message = error.Message });
    }
}
